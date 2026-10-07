// SEDevice.mm — what iOS says about the phone, for ThermalGovernor.cs: how hot
// it's running (NSProcessInfo's thermal state: 0 nominal, 1 fair, 2 serious,
// 3 critical - at serious and above iOS starts slowing it down).

#import <Foundation/Foundation.h>

extern "C" int SE_ThermalState()
{
    return (int)[NSProcessInfo processInfo].thermalState;
}
