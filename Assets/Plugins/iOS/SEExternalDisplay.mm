// SEExternalDisplay.mm — the TV's scene (TvOutput.cs). With Screen Mirroring on to an
// Apple TV, iOS hands the app the TV as a screen of its own only if Info.plist names a
// delegate for the external-display role (IosBuildPostprocessor adds it); otherwise it
// just mirrors the phone. Unity's DisplayManager makes the TV's window and puts it on
// the scene for that screen as the game activates the display - this only joins them
// if the scene comes after the window.
#import <UIKit/UIKit.h>
#include "Unity/DisplayManager.h"

@interface SEExternalDisplayScene : UIResponder <UIWindowSceneDelegate>
@property (strong, nonatomic) UIWindow *window;
@end

@implementation SEExternalDisplayScene

- (void)scene:(UIScene *)scene willConnectToSession:(UISceneSession *)session options:(UISceneConnectionOptions *)connectionOptions
{
    if (![scene isKindOfClass:[UIWindowScene class]]) return;
    UIWindowScene *windowScene = (UIWindowScene *)scene;
    DisplayConnection *conn = [DisplayManager Instance][windowScene.screen];
    if (conn && conn.window && !conn.window.hidden && !conn.window.windowScene)
        conn.window.windowScene = windowScene;
}

@end
