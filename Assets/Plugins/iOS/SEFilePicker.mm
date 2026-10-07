// SEFilePicker.mm — the iOS Files picker for NativeFilePicker.cs.
//
// Opens UIDocumentPickerViewController for zip files in copy mode, so iOS
// hands the app its own copies (in tmp/, no security-scoped access needed),
// then reports their paths to Unity as one newline-separated string.
//
// Or for a folder (SCAN FOLDER): the folder's opened in place, its access
// held (SE_EndFolderAccess lets it go once Unity has read what it needs),
// and every .zip in it and its subfolders reported - after a first line
// "?N", N the zips in iCloud not yet on the phone (they're asked for, to be
// there for a scan later).

#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

extern UIViewController* UnityGetGLViewController();
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

@interface SEFilePickerDelegate : NSObject <UIDocumentPickerDelegate>
@property (nonatomic, copy) NSString* gameObjectName;
@property (nonatomic, copy) NSString* callbackMethod;
@property (nonatomic) BOOL folder;
@end

// The picked folder, while its files are being read.
static NSURL* s_folder;

static void EndFolderAccess()
{
    if (s_folder) [s_folder stopAccessingSecurityScopedResource];
    s_folder = nil;
}

// The picker only keeps a weak reference to its delegate.
static SEFilePickerDelegate* s_delegate;

@implementation SEFilePickerDelegate

- (void)reply:(NSString*)paths
{
    UnitySendMessage(self.gameObjectName.UTF8String, self.callbackMethod.UTF8String, paths.UTF8String);
    s_delegate = nil;
}

- (void)scanFolder:(NSURL*)folder
{
    EndFolderAccess();
    if ([folder startAccessingSecurityScopedResource]) s_folder = folder;
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        NSFileManager* files = [NSFileManager defaultManager];
        NSMutableArray<NSString*>* zips = [NSMutableArray array];
        int inCloud = 0;
        NSDirectoryEnumerator* walk = [files enumeratorAtURL:folder includingPropertiesForKeys:nil
            options:NSDirectoryEnumerationSkipsPackageDescendants errorHandler:nil];
        for (NSURL* url in walk)
        {
            NSString* name = url.lastPathComponent.lowercaseString;
            if ([name hasPrefix:@"."] && [name hasSuffix:@".zip.icloud"])
            {
                inCloud++;
                [files startDownloadingUbiquitousItemAtURL:url error:nil];
            }
            else if (![name hasPrefix:@"."] && [name hasSuffix:@".zip"])
                [zips addObject:url.path];
        }
        NSString* reply = [NSString stringWithFormat:@"?%d\n%@", inCloud, [zips componentsJoinedByString:@"\n"]];
        dispatch_async(dispatch_get_main_queue(), ^{ [self reply:reply]; });
    });
}

- (void)documentPicker:(UIDocumentPickerViewController*)controller didPickDocumentsAtURLs:(NSArray<NSURL*>*)urls
{
    if (self.folder)
    {
        if (urls.count > 0) [self scanFolder:urls.firstObject];
        else [self reply:@""];
        return;
    }
    NSMutableArray<NSString*>* paths = [NSMutableArray array];
    for (NSURL* url in urls)
        if (url.isFileURL) [paths addObject:url.path];
    [self reply:[paths componentsJoinedByString:@"\n"]];
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController*)controller
{
    [self reply:@""];
}

@end

extern "C" void SE_PickZipFiles(const char* gameObjectName, const char* callbackMethod)
{
    s_delegate = [SEFilePickerDelegate new];
    s_delegate.gameObjectName = [NSString stringWithUTF8String:gameObjectName];
    s_delegate.callbackMethod = [NSString stringWithUTF8String:callbackMethod];

    UIDocumentPickerViewController* picker =
        [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:@[ UTTypeZIP ] asCopy:YES];
    picker.allowsMultipleSelection = YES;
    picker.delegate = s_delegate;
    [UnityGetGLViewController() presentViewController:picker animated:YES completion:nil];
}

extern "C" void SE_PickFolder(const char* gameObjectName, const char* callbackMethod)
{
    s_delegate = [SEFilePickerDelegate new];
    s_delegate.gameObjectName = [NSString stringWithUTF8String:gameObjectName];
    s_delegate.callbackMethod = [NSString stringWithUTF8String:callbackMethod];
    s_delegate.folder = YES;

    UIDocumentPickerViewController* picker =
        [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:@[ UTTypeFolder ] asCopy:NO];
    picker.delegate = s_delegate;
    [UnityGetGLViewController() presentViewController:picker animated:YES completion:nil];
}

extern "C" void SE_EndFolderAccess()
{
    EndFolderAccess();
}
