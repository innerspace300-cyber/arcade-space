// SEGameCenter.mm — Game Center for ENDLESS KNIGHT's leaderboard
// (GameCenter.cs): signs the player in (iOS shows its own sheet if needed),
// submits scores, and loads the global top entries, which go back to Unity
// (UnitySendMessage) as lines of "rank|name|score|me" (me 1 for the player),
// or "!message" if they can't be loaded.

#import <GameKit/GameKit.h>

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);
extern UIViewController* UnityGetGLViewController();

static void Send(NSString* object, NSString* method, NSString* message)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UnitySendMessage(object.UTF8String, method.UTF8String, message.UTF8String);
    });
}

// Sign-in's settled: signed in, or not (declined, offline, no account) -
// not while iOS's own sign-in sheet is up (StartupSplash waits for this).
static volatile bool s_settled;

extern "C" void SE_GCAuthenticate()
{
    GKLocalPlayer* player = GKLocalPlayer.localPlayer;
    if (player.authenticateHandler) return;
    player.authenticateHandler = ^(UIViewController* sheet, NSError* error) {
        if (sheet) { [UnityGetGLViewController() presentViewController:sheet animated:YES completion:nil]; return; }
        if (error) NSLog(@"[SEGameCenter] sign-in: %@", error);
        s_settled = true;
    };
}

extern "C" bool SE_GCSettled()
{
    return s_settled;
}

extern "C" bool SE_GCSignedIn()
{
    return GKLocalPlayer.localPlayer.isAuthenticated;
}

extern "C" void SE_GCSubmit(const char* leaderboard, long long score)
{
    if (!GKLocalPlayer.localPlayer.isAuthenticated || score <= 0) return;
    NSString* board = [NSString stringWithUTF8String:leaderboard];
    [GKLeaderboard submitScore:(NSInteger)score context:0 player:GKLocalPlayer.localPlayer
                leaderboardIDs:@[board] completionHandler:^(NSError* error) {
        if (error) NSLog(@"[SEGameCenter] submit: %@", error);
    }];
}

extern "C" void SE_GCLoadTop(const char* leaderboard, int count, const char* gameObject, const char* method)
{
    NSString* board = [NSString stringWithUTF8String:leaderboard];
    NSString* object = [NSString stringWithUTF8String:gameObject];
    NSString* callback = [NSString stringWithUTF8String:method];
    if (!GKLocalPlayer.localPlayer.isAuthenticated)
    {
        Send(object, callback, @"!Sign in to Game Center (iOS Settings > Game Center) to see the leaderboard");
        return;
    }
    [GKLeaderboard loadLeaderboardsWithIDs:@[board] completionHandler:^(NSArray<GKLeaderboard*>* boards, NSError* error) {
        GKLeaderboard* found = boards.firstObject;
        if (!found)
        {
            NSLog(@"[SEGameCenter] leaderboard: %@", error);
            Send(object, callback, @"!The leaderboard isn't available yet");
            return;
        }
        [found loadEntriesForPlayerScope:GKLeaderboardPlayerScopeGlobal timeScope:GKLeaderboardTimeScopeAllTime
                                   range:NSMakeRange(1, count)
                       completionHandler:^(GKLeaderboardEntry* mine, NSArray<GKLeaderboardEntry*>* entries, NSInteger total, NSError* loadError) {
            if (loadError && entries.count == 0)
            {
                NSLog(@"[SEGameCenter] entries: %@", loadError);
                Send(object, callback, @"!Couldn't load the leaderboard - try again in a moment");
                return;
            }
            NSMutableString* lines = [NSMutableString string];
            BOOL listed = NO;
            for (GKLeaderboardEntry* entry in entries)
            {
                BOOL me = mine && [entry.player.gamePlayerID isEqualToString:mine.player.gamePlayerID];
                listed |= me;
                NSString* name = [entry.player.displayName stringByReplacingOccurrencesOfString:@"|" withString:@"/"];
                [lines appendFormat:@"%ld|%@|%ld|%d\n", (long)entry.rank, name, (long)entry.score, me ? 1 : 0];
            }
            // The player's own place, if they're further down.
            if (mine && !listed)
            {
                NSString* name = [mine.player.displayName stringByReplacingOccurrencesOfString:@"|" withString:@"/"];
                [lines appendFormat:@"%ld|%@|%ld|1\n", (long)mine.rank, name, (long)mine.score];
            }
            Send(object, callback, lines);
        }];
    }];
}
