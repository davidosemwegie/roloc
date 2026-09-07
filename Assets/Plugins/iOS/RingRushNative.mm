#import <UIKit/UIKit.h>
#import <Security/Security.h>
#include <stdlib.h>
#include <string.h>

extern "C" UIViewController* UnityGetGLViewController(void);

static NSMutableDictionary* RRKeychainQuery(const char* key) {
    return [@{(__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
              (__bridge id)kSecAttrService: @"com.osazi.roloc.ring-rush",
              (__bridge id)kSecAttrAccount: [NSString stringWithUTF8String:key] ?: @""} mutableCopy];
}

extern "C" {
    const char* RRReadSecret(const char* key) {
        NSMutableDictionary* query = RRKeychainQuery(key);
        query[(__bridge id)kSecReturnData] = @YES;
        query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
        CFTypeRef result = NULL;
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
        if (status != errSecSuccess || !result) return strdup("");
        NSData* data = CFBridgingRelease(result);
        NSString* value = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        return strdup(value.UTF8String ?: "");
    }
    int RRSaveSecret(const char* key, const char* value) {
        NSMutableDictionary* query = RRKeychainQuery(key);
        NSData* data = [[NSString stringWithUTF8String:value] dataUsingEncoding:NSUTF8StringEncoding];
        NSDictionary* update = @{(__bridge id)kSecValueData: data};
        OSStatus status = SecItemUpdate((__bridge CFDictionaryRef)query, (__bridge CFDictionaryRef)update);
        if (status == errSecItemNotFound) {
            query[(__bridge id)kSecValueData] = data;
            query[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly;
            status = SecItemAdd((__bridge CFDictionaryRef)query, NULL);
        }
        return status == errSecSuccess;
    }
    void RRFreeString(void* value) { free(value); }
    int RRReduceMotion() { return UIAccessibilityIsReduceMotionEnabled() ? 1 : 0; }
    void RRHaptic(int strength) {
        dispatch_async(dispatch_get_main_queue(), ^{
            UIImpactFeedbackGenerator* generator = [[UIImpactFeedbackGenerator alloc]
                initWithStyle:strength ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleLight];
            [generator prepare];
            [generator impactOccurredWithIntensity:strength ? 0.7 : 0.4];
        });
    }
    void RRShare(const char* text, const char* imagePath) {
        NSString* message = [NSString stringWithUTF8String:text] ?: @"";
        NSString* path = [NSString stringWithUTF8String:imagePath] ?: @"";
        dispatch_async(dispatch_get_main_queue(), ^{
            UIViewController* presenter = UnityGetGLViewController();
            if (!presenter || presenter.presentedViewController) return;
            NSMutableArray* items = [NSMutableArray arrayWithObject:message];
            if (path.length && [[NSFileManager defaultManager] fileExistsAtPath:path])
                [items addObject:[NSURL fileURLWithPath:path]];
            UIActivityViewController* sheet = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
            sheet.popoverPresentationController.sourceView = presenter.view;
            sheet.popoverPresentationController.sourceRect = CGRectMake(presenter.view.bounds.size.width/2, presenter.view.bounds.size.height/2, 1, 1);
            [presenter presentViewController:sheet animated:YES completion:nil];
        });
    }
}
