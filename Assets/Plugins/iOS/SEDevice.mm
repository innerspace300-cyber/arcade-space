// SEDevice.mm — what iOS says about the phone, for ThermalGovernor.cs: how hot
// it's running (NSProcessInfo's thermal state: 0 nominal, 1 fair, 2 serious,
// 3 critical - at serious and above iOS starts slowing it down).

#import <Foundation/Foundation.h>

extern "C" int SE_ThermalState()
{
    return (int)[NSProcessInfo processInfo].thermalState;
}

// The connected controller's battery, for Settings' controller row (GameController's GCDeviceBattery,
// iOS 14+): its level 0-100, or -1 when there's no controller or it doesn't say. `charging` set when it's
// charging (or full on the cable).
#import <GameController/GameController.h>

extern "C" int SE_ControllerBattery(int* charging)
{
    if (charging) *charging = 0;
    GCController* pad = GCController.current;
    if (!pad) pad = GCController.controllers.firstObject;
    if (!pad) return -1;
    GCDeviceBattery* battery = pad.battery;
    if (!battery || battery.batteryState == GCDeviceBatteryStateUnknown) return -1;
    if (charging) *charging = (battery.batteryState == GCDeviceBatteryStateCharging || battery.batteryState == GCDeviceBatteryStateFull) ? 1 : 0;
    return (int)lroundf(battery.batteryLevel * 100.0f);
}

// The controller's light bar (a DualSense's: GCDeviceLight, iOS 14+) set to this colour, 0..1 each - dim, to spare
// its battery. 0 when there's no controller or it has no light.
extern "C" int SE_ControllerLight(float r, float g, float b)
{
    GCController* pad = GCController.current;
    if (!pad) pad = GCController.controllers.firstObject;
    if (!pad || !pad.light) return 0;
    pad.light.color = [[GCColor alloc] initWithRed:r green:g blue:b];
    return 1;
}

// A controller the phone sits in - a Razer Kishi, a Backbone - rather than one across the room on
// Bluetooth (TvOutput: the phone turns on its side and plays like a TV). 1 if any connected one is.
extern "C" int SE_ControllerAttached()
{
    for (GCController* pad in GCController.controllers)
    {
        if (pad.isAttachedToDevice) return 1;
        NSString* name = [NSString stringWithFormat:@"%@ %@", pad.vendorName ?: @"", pad.productCategory ?: @""].lowercaseString;
        if ([name containsString:@"kishi"] || [name containsString:@"backbone"]) return 1;
    }
    return 0;
}

// How many controllers iOS has connected (GamepadInput: Unity's list can keep one that's gone - a Kishi taken off).
extern "C" int SE_ControllerCount()
{
    return (int)GCController.controllers.count;
}
