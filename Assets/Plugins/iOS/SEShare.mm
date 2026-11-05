// SEShare.mm — iOS's share sheet for ScreenCap.cs: a picture (a PNG the app
// wrote) offered to whatever on the phone takes pictures - AirDrop,
// Messages, Mail, Save Image (Photos), and apps like Instagram, Snapchat
// and Gmail when they're installed.

#import <UIKit/UIKit.h>
#import <Photos/Photos.h>

extern UIViewController* UnityGetGLViewController();

// The controller on top of everything (a sheet or banner already up - Game
// Center's, say - would make presenting from Unity's root silently fail).
static UIViewController* SETopController()
{
    UIViewController* top = UnityGetGLViewController();
    while (top.presentedViewController && !top.presentedViewController.isBeingDismissed) top = top.presentedViewController;
    return top;
}

// Presents `sheet` from the top controller; if that's mid-animation (or not
// on screen yet), tries again a moment later, a few times.
static void SEPresent(UIViewController* sheet, int tries, void (^failed)(void))
{
    UIViewController* top = SETopController();
    if (!top || top.isBeingPresented || top.isBeingDismissed || !top.view.window)
    {
        if (tries > 0)
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.3 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{ SEPresent(sheet, tries - 1, failed); });
        else if (failed) failed();   // (never got on screen: let the game go on)
        return;
    }
    // (On an iPad the sheet is a popover: from the middle of the screen.)
    sheet.popoverPresentationController.sourceView = top.view;
    sheet.popoverPresentationController.sourceRect = CGRectMake(CGRectGetMidX(top.view.bounds), CGRectGetMidY(top.view.bounds), 1, 1);
    sheet.popoverPresentationController.permittedArrowDirections = 0;
    [top presentViewController:sheet animated:YES completion:nil];
}
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

extern "C" void SE_ShareImage(const char* path)
{
    UIImage* image = [UIImage imageWithContentsOfFile:[NSString stringWithUTF8String:path]];
    if (!image) return;
    UIViewController* root = UnityGetGLViewController();
    UIActivityViewController* share = [[UIActivityViewController alloc] initWithActivityItems:@[ image ] applicationActivities:nil];
    // (On an iPad the sheet is a popover: from the middle of the screen.)
    share.popoverPresentationController.sourceView = root.view;
    share.popoverPresentationController.sourceRect = CGRectMake(CGRectGetMidX(root.view.bounds), CGRectGetMidY(root.view.bounds), 1, 1);
    share.popoverPresentationController.permittedArrowDirections = 0;
    [root presentViewController:share animated:YES completion:nil];
}

// YouTube in the sheet for a video (its app doesn't offer itself there):
// the video's saved to Photos, then the YouTube app opens (YouTube's upload
// page in Safari without it), to upload it from there.
@interface SEYouTubeActivity : UIActivity
@property (nonatomic, strong) NSURL* video;
@end

@implementation SEYouTubeActivity
+ (UIActivityCategory)activityCategory { return UIActivityCategoryShare; }
- (UIActivityType)activityType { return @"com.waynelamb.arcade.youtube"; }
- (NSString*)activityTitle { return @"YouTube"; }
- (UIImage*)activityImage
{
    // A play triangle in a rounded box.
    UIGraphicsImageRenderer* draw = [[UIGraphicsImageRenderer alloc] initWithSize:CGSizeMake(60, 60)];
    return [draw imageWithActions:^(UIGraphicsImageRendererContext* context) {
        [[UIColor colorWithRed:1 green:0 blue:0 alpha:1] setFill];
        [[UIBezierPath bezierPathWithRoundedRect:CGRectMake(6, 14, 48, 34) cornerRadius:10] fill];
        UIBezierPath* play = [UIBezierPath bezierPath];
        [play moveToPoint:CGPointMake(25, 22)];
        [play addLineToPoint:CGPointMake(39, 31)];
        [play addLineToPoint:CGPointMake(25, 40)];
        [play closePath];
        [[UIColor whiteColor] setFill];
        [play fill];
    }];
}
- (BOOL)canPerformWithActivityItems:(NSArray*)items
{
    for (id item in items) if ([item isKindOfClass:[NSURL class]]) return YES;
    return NO;
}
- (void)prepareWithActivityItems:(NSArray*)items
{
    for (id item in items) if ([item isKindOfClass:[NSURL class]]) self.video = item;
}
- (void)performActivity
{
    NSURL* video = self.video;
    void (^open)(void) = ^{
        dispatch_async(dispatch_get_main_queue(), ^{
            NSURL* app = [NSURL URLWithString:@"youtube://"];
            NSURL* web = [NSURL URLWithString:@"https://www.youtube.com/upload"];
            [[UIApplication sharedApplication] openURL:app options:@{} completionHandler:^(BOOL opened) {
                if (!opened) [[UIApplication sharedApplication] openURL:web options:@{} completionHandler:nil];
            }];
            [self activityDidFinish:YES];
        });
    };
    [PHPhotoLibrary requestAuthorizationForAccessLevel:PHAccessLevelAddOnly handler:^(PHAuthorizationStatus status) {
        if (status != PHAuthorizationStatusAuthorized && status != PHAuthorizationStatusLimited) { open(); return; }
        [[PHPhotoLibrary sharedPhotoLibrary] performChanges:^{
            [PHAssetChangeRequest creationRequestForAssetFromVideoAtFileURL:video];
        } completionHandler:^(BOOL success, NSError* error) {
            if (!success) NSLog(@"[SEShare] saving for YouTube: %@", error);
            open();
        }];
    }];
}
@end

// A file (the CAP key's video): the sheet offers it as a movie - Save Video,
// AirDrop, Messages and the like, and YouTube (above). When the sheet's
// gone, `method` is sent to the object called `gameObject` (the game's
// paused while it's up).
extern "C" void SE_ShareFile(const char* path, const char* gameObject, const char* method)
{
    NSString* target = [NSString stringWithUTF8String:gameObject];
    NSString* callback = [NSString stringWithUTF8String:method];
    NSURL* url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
    UIActivityViewController* share = [[UIActivityViewController alloc] initWithActivityItems:@[ url ] applicationActivities:@[ [SEYouTubeActivity new] ]];
    share.completionWithItemsHandler = ^(UIActivityType type, BOOL completed, NSArray* items, NSError* error) {
        UnitySendMessage(target.UTF8String, callback.UTF8String, "");
    };
    dispatch_async(dispatch_get_main_queue(), ^{ SEPresent(share, 10, ^{ UnitySendMessage(target.UTF8String, callback.UTF8String, ""); }); });
}

// The video at `path` saved to Photos (ShareWindow.cs, for a controller: nothing to tap in
// the sheet); "1" or "0" sent to `method` on `gameObject` when it's done.
extern "C" void SE_SaveVideo(const char* path, const char* gameObject, const char* method)
{
    NSString* target = [NSString stringWithUTF8String:gameObject];
    NSString* callback = [NSString stringWithUTF8String:method];
    NSURL* url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
    void (^reply)(BOOL) = ^(BOOL ok) {
        dispatch_async(dispatch_get_main_queue(), ^{ UnitySendMessage(target.UTF8String, callback.UTF8String, ok ? "1" : "0"); });
    };
    [PHPhotoLibrary requestAuthorizationForAccessLevel:PHAccessLevelAddOnly handler:^(PHAuthorizationStatus status) {
        if (status != PHAuthorizationStatusAuthorized && status != PHAuthorizationStatusLimited) { reply(NO); return; }
        [[PHPhotoLibrary sharedPhotoLibrary] performChanges:^{
            [PHAssetChangeRequest creationRequestForAssetFromVideoAtFileURL:url];
        } completionHandler:^(BOOL success, NSError* error) {
            if (!success) NSLog(@"[SEShare] saving the video: %@", error);
            reply(success);
        }];
    }];
}
