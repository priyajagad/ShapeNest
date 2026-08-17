#import <UIKit/UIKit.h>

extern "C"
{
    static UIImpactFeedbackGenerator *sLight;
    static UIImpactFeedbackGenerator *sMedium;
    static UIImpactFeedbackGenerator *sHeavy;
    static UISelectionFeedbackGenerator *sSelection;
    static UINotificationFeedbackGenerator *sNotification;

    static void ShapeNest_EnsureGenerators(void)
    {
        if (sLight == nil)
        {
            sLight = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            sMedium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
            sHeavy = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
            sSelection = [[UISelectionFeedbackGenerator alloc] init];
            sNotification = [[UINotificationFeedbackGenerator alloc] init];
        }
    }

    void ShapeNest_PlayImpact(int style)
    {
        if (@available(iOS 10.0, *))
        {
            ShapeNest_EnsureGenerators();
            UIImpactFeedbackGenerator *generator = sLight;
            if (style == 1)
            {
                generator = sMedium;
            }
            else if (style == 2)
            {
                generator = sHeavy;
            }

            [generator prepare];
            [generator impactOccurred];
        }
    }

    void ShapeNest_PlaySelection(void)
    {
        if (@available(iOS 10.0, *))
        {
            ShapeNest_EnsureGenerators();
            [sSelection prepare];
            [sSelection selectionChanged];
        }
    }

    void ShapeNest_PlayNotification(int type)
    {
        if (@available(iOS 10.0, *))
        {
            ShapeNest_EnsureGenerators();
            UINotificationFeedbackType feedbackType = UINotificationFeedbackTypeWarning;
            if (type == 0)
            {
                feedbackType = UINotificationFeedbackTypeSuccess;
            }
            else if (type == 2)
            {
                feedbackType = UINotificationFeedbackTypeError;
            }

            [sNotification prepare];
            [sNotification notificationOccurred:feedbackType];
        }
    }
}
