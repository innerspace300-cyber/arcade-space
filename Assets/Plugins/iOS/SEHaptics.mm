// SEHaptics.mm — ARcade's haptics: iOS's own taps (UIImpactFeedbackGenerator,
// UINotificationFeedbackGenerator, UISelectionFeedbackGenerator) for Haptics.cs.
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *s_impact[5];
static UINotificationFeedbackGenerator *s_notify;
static UISelectionFeedbackGenerator *s_select;

extern "C" {

// style: 0 light, 1 medium, 2 heavy, 3 soft, 4 rigid; intensity 0..1.
void _SEHapticImpact(int style, float intensity)
{
    if (style < 0 || style > 4) style = 1;
    if (!s_impact[style])
    {
        UIImpactFeedbackStyle styles[] = { UIImpactFeedbackStyleLight, UIImpactFeedbackStyleMedium, UIImpactFeedbackStyleHeavy,
                                           UIImpactFeedbackStyleSoft, UIImpactFeedbackStyleRigid };
        s_impact[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:styles[style]];
    }
    [s_impact[style] impactOccurredWithIntensity:MAX(0.0f, MIN(1.0f, intensity))];
    [s_impact[style] prepare];
}

// type: 0 success, 1 warning, 2 error.
void _SEHapticNotify(int type)
{
    if (!s_notify) s_notify = [[UINotificationFeedbackGenerator alloc] init];
    UINotificationFeedbackType types[] = { UINotificationFeedbackTypeSuccess, UINotificationFeedbackTypeWarning, UINotificationFeedbackTypeError };
    [s_notify notificationOccurred:types[MAX(0, MIN(2, type))]];
    [s_notify prepare];
}

void _SEHapticSelection(void)
{
    if (!s_select) s_select = [[UISelectionFeedbackGenerator alloc] init];
    [s_select selectionChanged];
    [s_select prepare];
}

}

// ---- The controller's rumble (a DualSense, Xbox or other controller with haptics: GCController.haptics, iOS 14+) ----
#import <GameController/GameController.h>
#import <CoreHaptics/CoreHaptics.h>

static CHHapticEngine *s_padEngine;
static const void *s_padFor;   // (which controller it was made for: compared, never used)

static CHHapticEngine *PadEngine(void)
{
    GCController *pad = GCController.current ?: GCController.controllers.firstObject;
    if (!pad || !pad.haptics) { s_padEngine = nil; s_padFor = NULL; return nil; }
    if (s_padEngine && s_padFor == (__bridge const void *)pad) return s_padEngine;
    s_padEngine = [pad.haptics createEngineWithLocality:GCHapticsLocalityDefault];
    s_padFor = (__bridge const void *)pad;
    if (!s_padEngine) return nil;
    s_padEngine.playsHapticsOnly = YES;
    s_padEngine.autoShutdownEnabled = YES;
    s_padEngine.resetHandler = ^{ [s_padEngine startAndReturnError:nil]; };
    s_padEngine.stoppedHandler = ^(CHHapticEngineStoppedReason reason) { };
    [s_padEngine startAndReturnError:nil];
    return s_padEngine;
}

extern "C" {

// intensity and sharpness 0..1; seconds 0 = a single tap, else a buzz that long (fading out; negative: that long,
// held level - one of a run of them, a buzz kept up).
void _SEPadRumble(float intensity, float sharpness, float seconds)
{
    bool fade = seconds > 0.0f;
    seconds = fabsf(seconds);
    if (@available(iOS 14.0, *))
    {
        CHHapticEngine *engine = PadEngine();
        if (!engine) return;
        intensity = MAX(0.0f, MIN(1.0f, intensity));
        sharpness = MAX(0.0f, MIN(1.0f, sharpness));
        NSArray *params = @[[[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:intensity],
                            [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:sharpness]];
        CHHapticEvent *event = seconds <= 0.0f
            ? [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticTransient parameters:params relativeTime:0]
            : [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticContinuous parameters:params relativeTime:0 duration:seconds];
        NSMutableArray *curves = [NSMutableArray array];
        if (fade)
        {
            // (easing off toward its end)
            NSArray *points = @[[[CHHapticParameterCurveControlPoint alloc] initWithRelativeTime:0 value:1.0f],
                                [[CHHapticParameterCurveControlPoint alloc] initWithRelativeTime:seconds * 0.6 value:0.8f],
                                [[CHHapticParameterCurveControlPoint alloc] initWithRelativeTime:seconds value:0.0f]];
            [curves addObject:[[CHHapticParameterCurve alloc] initWithParameterID:CHHapticDynamicParameterIDHapticIntensityControl controlPoints:points relativeTime:0]];
        }
        NSError *error = nil;
        CHHapticPattern *pattern = [[CHHapticPattern alloc] initWithEvents:@[event] parameterCurves:curves error:&error];
        if (!pattern) return;
        id<CHHapticPatternPlayer> player = [engine createPlayerWithPattern:pattern error:&error];
        if (!player) { s_padEngine = nil; return; }   // (the engine went away: made afresh next time)
        if (![player startAtTime:CHHapticTimeImmediate error:&error]) s_padEngine = nil;
    }
}

}

extern "C" int _SEPadHasHaptics(void)
{
    if (@available(iOS 14.0, *))
    {
        GCController *pad = GCController.current ?: GCController.controllers.firstObject;
        return pad && pad.haptics ? 1 : 0;
    }
    return 0;
}
