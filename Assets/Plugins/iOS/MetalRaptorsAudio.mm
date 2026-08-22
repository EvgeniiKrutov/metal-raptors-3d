#import <AVFoundation/AVFoundation.h>

extern "C" int MetalRaptorsConfigureAudioSession(int allowMixing)
{
    @autoreleasepool
    {
        AVAudioSession* session = [AVAudioSession sharedInstance];
        NSError* error = nil;

        NSLog(@"[MetalRaptors] audio session in: %@ options %lu, %ld of %ld channels",
            session.category, (unsigned long)session.categoryOptions,
            (long)session.outputNumberOfChannels, (long)session.maximumOutputNumberOfChannels);

        AVAudioSessionCategoryOptions options =
            allowMixing ? AVAudioSessionCategoryOptionMixWithOthers
                        : (AVAudioSessionCategoryOptions)0;

        if (![session setCategory: AVAudioSessionCategoryPlayback
                             mode: AVAudioSessionModeDefault
                          options: options
                            error: &error])
        {
            NSLog(@"[MetalRaptors] setCategory failed: %@", error);
        }

        if (![session setActive: YES error: &error])
        {
            NSLog(@"[MetalRaptors] setActive failed: %@", error);
        }

        if (session.maximumOutputNumberOfChannels >= 2
            && ![session setPreferredOutputNumberOfChannels: 2 error: &error])
        {
            NSLog(@"[MetalRaptors] setPreferredOutputNumberOfChannels failed: %@", error);
        }

        NSLog(@"[MetalRaptors] audio session out: %@ options %lu, %ld channels",
            session.category, (unsigned long)session.categoryOptions,
            (long)session.outputNumberOfChannels);

        return (int)session.outputNumberOfChannels;
    }
}
