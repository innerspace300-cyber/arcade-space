// SERecorder.mm — the CAP key's video (ScreenRecorder.cs): frames Unity
// reads back from the GPU (BGRA rows, bottom-up) and the game's own sound
// (Unity's mix, float samples) written to an .mp4 - H.264 and AAC, through
// AVAssetWriter. Frames are timed by the clock as they arrive, the sound by
// its sample count from when it first came. Appends happen on one serial
// queue; the sound arrives on Unity's audio thread. When it's finished the
// file's path is sent back to Unity (UnitySendMessage), or "" if it failed.

#import <AVFoundation/AVFoundation.h>
#import <QuartzCore/QuartzCore.h>

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

static AVAssetWriter* s_writer;
static AVAssetWriterInput* s_video;
static AVAssetWriterInput* s_audio;
static AVAssetWriterInputPixelBufferAdaptor* s_adaptor;
static dispatch_queue_t s_queue;
static CMAudioFormatDescriptionRef s_audioFormat;
static NSString* s_path;
static double s_start;
static double s_lastFrame;
static double s_audioStart;
static int64_t s_audioFrames;
static int s_rate, s_channels, s_width, s_height;
static volatile bool s_running;

extern "C" bool SE_RecStart(const char* path, int width, int height, int fps, int sampleRate, int channels)
{
    if (s_running) return false;
    s_path = [NSString stringWithUTF8String:path];
    [[NSFileManager defaultManager] removeItemAtPath:s_path error:nil];
    NSError* error = nil;
    s_writer = [AVAssetWriter assetWriterWithURL:[NSURL fileURLWithPath:s_path] fileType:AVFileTypeMPEG4 error:&error];
    if (!s_writer) { NSLog(@"[SERecorder] %@", error); return false; }
    // (Its index at the front: what social apps' importers expect.)
    s_writer.shouldOptimizeForNetworkUse = YES;

    s_width = width; s_height = height;
    NSDictionary* compression = @{
        AVVideoAverageBitRateKey: @(MAX(4000000, width * height * 6)),
        AVVideoExpectedSourceFrameRateKey: @(fps),
        AVVideoMaxKeyFrameIntervalKey: @(fps * 2),
        AVVideoProfileLevelKey: AVVideoProfileLevelH264HighAutoLevel,
    };
    s_video = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeVideo outputSettings:@{
        AVVideoCodecKey: AVVideoCodecTypeH264,
        AVVideoWidthKey: @(width),
        AVVideoHeightKey: @(height),
        AVVideoCompressionPropertiesKey: compression,
    }];
    s_video.expectsMediaDataInRealTime = YES;
    s_adaptor = [AVAssetWriterInputPixelBufferAdaptor assetWriterInputPixelBufferAdaptorWithAssetWriterInput:s_video sourcePixelBufferAttributes:@{
        (id)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
        (id)kCVPixelBufferWidthKey: @(width),
        (id)kCVPixelBufferHeightKey: @(height),
    }];
    if ([s_writer canAddInput:s_video]) [s_writer addInput:s_video];

    s_rate = sampleRate; s_channels = channels;
    s_audio = nil;
    if (sampleRate > 0 && channels > 0)
    {
        AudioStreamBasicDescription pcm = {0};
        pcm.mSampleRate = sampleRate;
        pcm.mFormatID = kAudioFormatLinearPCM;
        pcm.mFormatFlags = kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked;
        pcm.mChannelsPerFrame = channels;
        pcm.mBitsPerChannel = 32;
        pcm.mBytesPerFrame = 4 * channels;
        pcm.mFramesPerPacket = 1;
        pcm.mBytesPerPacket = pcm.mBytesPerFrame;
        if (s_audioFormat) { CFRelease(s_audioFormat); s_audioFormat = NULL; }
        CMAudioFormatDescriptionCreate(kCFAllocatorDefault, &pcm, 0, NULL, 0, NULL, NULL, &s_audioFormat);
        s_audio = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeAudio outputSettings:@{
            AVFormatIDKey: @(kAudioFormatMPEG4AAC),
            AVSampleRateKey: @(sampleRate),
            AVNumberOfChannelsKey: @(MIN(channels, 2)),
            AVEncoderBitRateKey: @(128000),
        }];
        s_audio.expectsMediaDataInRealTime = YES;
        if (channels > 2 || ![s_writer canAddInput:s_audio]) s_audio = nil;
        else [s_writer addInput:s_audio];
    }

    if (![s_writer startWriting]) { NSLog(@"[SERecorder] %@", s_writer.error); s_writer = nil; return false; }
    [s_writer startSessionAtSourceTime:kCMTimeZero];
    if (!s_queue) s_queue = dispatch_queue_create("ARcade.recorder", DISPATCH_QUEUE_SERIAL);
    s_start = CACurrentMediaTime();
    s_lastFrame = -1;
    s_audioStart = -1;
    s_audioFrames = 0;
    s_running = true;
    return true;
}

// One frame: width x height BGRA, rows bottom-up (as Unity reads them back).
extern "C" void SE_RecVideoFrame(const unsigned char* bgra, int width, int height)
{
    if (!s_running || width != s_width || height != s_height) return;
    double t = CACurrentMediaTime() - s_start;
    if (t <= s_lastFrame) return;
    if (!s_video.readyForMoreMediaData || !s_adaptor.pixelBufferPool) return;   // (behind: drop it)
    CVPixelBufferRef buffer = NULL;
    if (CVPixelBufferPoolCreatePixelBuffer(NULL, s_adaptor.pixelBufferPool, &buffer) != kCVReturnSuccess) return;
    CVPixelBufferLockBaseAddress(buffer, 0);
    unsigned char* dst = (unsigned char*)CVPixelBufferGetBaseAddress(buffer);
    size_t stride = CVPixelBufferGetBytesPerRow(buffer), row = (size_t)width * 4;
    for (int y = 0; y < height; y++)
        memcpy(dst + (size_t)y * stride, bgra + (size_t)(height - 1 - y) * row, row);
    CVPixelBufferUnlockBaseAddress(buffer, 0);
    s_lastFrame = t;
    CMTime time = CMTimeMakeWithSeconds(t, 600);
    dispatch_async(s_queue, ^{
        if (s_running && s_video.readyForMoreMediaData) [s_adaptor appendPixelBuffer:buffer withPresentationTime:time];
        CVPixelBufferRelease(buffer);
    });
}

// Sound from Unity's audio thread: `count` floats, interleaved.
extern "C" void SE_RecAudio(const float* samples, int count, int channels)
{
    if (!s_running || !s_audio || channels != s_channels || count <= 0) return;
    double now = CACurrentMediaTime() - s_start;
    NSData* copy = [NSData dataWithBytes:samples length:(size_t)count * sizeof(float)];
    dispatch_async(s_queue, ^{
        if (!s_running || !s_audio.readyForMoreMediaData) return;
        if (s_audioStart < 0) s_audioStart = now;
        CMItemCount frames = count / channels;
        CMTime time = CMTimeMake((int64_t)llround(s_audioStart * s_rate) + s_audioFrames, s_rate);
        s_audioFrames += frames;
        CMBlockBufferRef block = NULL;
        if (CMBlockBufferCreateWithMemoryBlock(kCFAllocatorDefault, NULL, copy.length, kCFAllocatorDefault, NULL, 0, copy.length, 0, &block) != kCMBlockBufferNoErr) return;
        CMBlockBufferReplaceDataBytes(copy.bytes, block, 0, copy.length);
        CMSampleBufferRef sample = NULL;
        if (CMAudioSampleBufferCreateReadyWithPacketDescriptions(kCFAllocatorDefault, block, s_audioFormat, frames, time, NULL, &sample) == noErr)
        {
            [s_audio appendSampleBuffer:sample];
            CFRelease(sample);
        }
        CFRelease(block);
    });
}

extern "C" void SE_RecStop(const char* gameObject, const char* method)
{
    NSString* object = [NSString stringWithUTF8String:gameObject];
    NSString* callback = [NSString stringWithUTF8String:method];
    if (!s_running) { UnitySendMessage(object.UTF8String, callback.UTF8String, ""); return; }
    s_running = false;
    dispatch_async(s_queue, ^{
        [s_video markAsFinished];
        if (s_audio) [s_audio markAsFinished];
        AVAssetWriter* writer = s_writer;
        NSString* path = s_path;
        [writer finishWritingWithCompletionHandler:^{
            BOOL ok = writer.status == AVAssetWriterStatusCompleted;
            if (!ok) NSLog(@"[SERecorder] %@", writer.error);
            dispatch_async(dispatch_get_main_queue(), ^{
                UnitySendMessage(object.UTF8String, callback.UTF8String, ok ? path.UTF8String : "");
            });
        }];
    });
}
