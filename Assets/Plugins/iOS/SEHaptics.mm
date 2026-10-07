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
