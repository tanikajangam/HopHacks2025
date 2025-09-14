import { supabase } from "@/integrations/supabase/client";

interface EmailNotificationParams {
  userEmail: string;
  userName: string;
  type: 'upload' | 'deletion';
  files: string[];
  timestamp?: string;
}

export const sendEmailNotification = async ({
  userEmail,
  userName,
  type,
  files,
  timestamp = new Date().toISOString()
}: EmailNotificationParams): Promise<boolean> => {
  try {
    const { data, error } = await supabase.functions.invoke('send-notification-email', {
      body: {
        userEmail,
        userName,
        type,
        files,
        timestamp
      }
    });

    if (error) {
      console.error('Error sending email notification:', error);
      return false;
    }

    console.log('Email notification sent successfully:', data);
    return true;
  } catch (error) {
    console.error('Failed to send email notification:', error);
    return false;
  }
};