#import <UIKit/UIKit.h>
#import <UserMessagingPlatform/UserMessagingPlatform.h>
#import <UnityAds/UnityAds.h>
#import <UnityAds/UnityAds-Swift.h>

extern "C" UIViewController* UnityGetGLViewController(void);
typedef void (*RRAdConsentCallback)(int request, int flags);
static BOOL RRConsentBusy = NO;

static BOOL RRConsentPurpose(NSString* purposes, NSUInteger oneBasedPurpose) {
    return purposes.length >= oneBasedPurpose && [purposes characterAtIndex:oneBasedPurpose - 1] == '1';
}

static BOOL RRAdditionalConsentContains(NSString* consent, NSString* provider) {
    NSArray<NSString*>* sections = [consent componentsSeparatedByString:@"~"];
    if (sections.count < 2 || ![sections[0] isEqualToString:@"2"]) return NO;
    // Only the consented section counts. The optional dv section records disclosure, not consent.
    return [[sections[1] componentsSeparatedByString:@"."] containsObject:provider];
}

static int RRConsentFlags(BOOL validUpdate) {
    UMPConsentInformation* info = UMPConsentInformation.sharedInstance;
    BOOL options = info.privacyOptionsRequirementStatus == UMPPrivacyOptionsRequirementStatusRequired;
    int flags = options ? 2 : 0;
    if (!validUpdate || !info.canRequestAds) return flags;

    NSUserDefaults* defaults = NSUserDefaults.standardUserDefaults;
    id gdprValue = [defaults objectForKey:@"IABTCF_gdprApplies"];
    BOOL gdprApplies = [gdprValue isKindOfClass:NSNumber.class] && [gdprValue integerValue] == 1;
    BOOL gdprDoesNotApply = [gdprValue isKindOfClass:NSNumber.class] && [gdprValue integerValue] == 0;
    NSString* purposes = [defaults stringForKey:@"IABTCF_PurposeConsents"];
    if (gdprApplies) {
        // UMP completion alone also covers a refusal. LevelPlay still requires storage consent.
        if (!RRConsentPurpose(purposes, 1)) return flags;
        flags |= 1;
        NSString* additional = [defaults stringForKey:@"IABTCF_AddtlConsent"];
        NSString* vendors = [defaults stringForKey:@"IABTCF_VendorConsents"];
        // Unity Ads is TCF vendor 1549; ironSource remains Additional Consent provider 2878.
        // Never let legacy Unity AC consent override a current TCF refusal.
        BOOL unityConsented = vendors.length >= 1549 && [vendors characterAtIndex:1548] == '1';
        if (RRConsentPurpose(purposes, 3) && RRConsentPurpose(purposes, 4)
            && unityConsented
            && RRAdditionalConsentContains(additional, @"2878")) flags |= 4;
        return flags;
    }
    // A fresh provider response may legitimately have no TC string outside GDPR regions.
    if (!gdprDoesNotApply && info.consentStatus != UMPConsentStatusNotRequired) return flags;
    flags |= 1;
    // Until US GPP purpose/opt-out parsing is verified, retain contextual ads in regions
    // requiring a privacy-options entry. This never converts an opt-out into consent.
    if (!options) flags |= 4;
    return flags;
}

extern "C" void RRAdConsentRequest(int request, int privacyOptions, RRAdConsentCallback callback) {
    if (!callback) return;
    dispatch_async(dispatch_get_main_queue(), ^{
        if (RRConsentBusy || UIApplication.sharedApplication.applicationState != UIApplicationStateActive) {
            callback(request, 0);
            return;
        }
        RRConsentBusy = YES;
        __block BOOL finished = NO;
        void (^finish)(BOOL) = ^(BOOL validUpdate) {
            dispatch_async(dispatch_get_main_queue(), ^{
                if (finished) return;
                finished = YES;
                RRConsentBusy = NO;
                callback(request, RRConsentFlags(validUpdate));
            });
        };
        void (^present)(void) = ^{
            if (UIApplication.sharedApplication.applicationState != UIApplicationStateActive) {
                finish(NO);
                return;
            }
            if (privacyOptions) {
                if (UMPConsentInformation.sharedInstance.privacyOptionsRequirementStatus != UMPPrivacyOptionsRequirementStatusRequired) {
                    finish(YES);
                    return;
                }
                [UMPConsentForm presentPrivacyOptionsFormFromViewController:UnityGetGLViewController()
                    completionHandler:^(NSError* error) { finish(error == nil); }];
            } else {
                [UMPConsentForm loadAndPresentIfRequiredFromViewController:UnityGetGLViewController()
                    completionHandler:^(NSError* error) { finish(error == nil); }];
            }
        };
        UMPRequestParameters* parameters = [[UMPRequestParameters alloc] init];
        // Region comes from the provider. Never infer it from language or timezone.
        [UMPConsentInformation.sharedInstance requestConsentInfoUpdateWithParameters:parameters
            completionHandler:^(NSError* error) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    if (error) { finish(NO); return; }
                    present();
                });
            }];
    });
}

extern "C" void RRAdConsentSetTrackingAllowed(int trackingAllowed) {
    void (^apply)(void) = ^{ [UnityAds setNonBehavioral:trackingAllowed == 0]; };
    // Apply before LevelPlay initialization, including when called from Unity's scripting thread.
    if (NSThread.isMainThread) apply();
    else dispatch_sync(dispatch_get_main_queue(), apply);
}
