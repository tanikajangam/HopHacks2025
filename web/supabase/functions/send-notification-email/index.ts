import { serve } from "https://deno.land/std@0.190.0/http/server.ts";
import { Resend } from "npm:resend@2.0.0";
import { createClient } from 'https://esm.sh/@supabase/supabase-js@2.57.4';

const resend = new Resend(Deno.env.get("RESEND_API_KEY"));

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers":
    "authorization, x-client-info, apikey, content-type",
};

interface NotificationEmailRequest {
  userEmail: string;
  userName: string;
  type: 'upload' | 'deletion';
  files: string[];
  timestamp: string;
}

const handler = async (req: Request): Promise<Response> => {
  // Handle CORS preflight requests
  if (req.method === "OPTIONS") {
    return new Response(null, { headers: corsHeaders });
  }

  try {
    // Verify authentication
    const authHeader = req.headers.get('authorization');
    if (!authHeader) {
      throw new Error('No authorization header');
    }

    const supabase = createClient(
      Deno.env.get('SUPABASE_URL') ?? '',
      Deno.env.get('SUPABASE_ANON_KEY') ?? '',
      {
        global: {
          headers: { Authorization: authHeader },
        },
      }
    );

    const { data: { user }, error: authError } = await supabase.auth.getUser();
    if (authError || !user) {
      throw new Error('Unauthorized');
    }

    const { userEmail, userName, type, files, timestamp }: NotificationEmailRequest = await req.json();

    // Validate input
    if (!userEmail || !userName || !type || !files || !timestamp) {
      throw new Error('Missing required fields');
    }

    console.log(`Sending ${type} notification to ${userEmail} for ${files.length} files`);

    const isUpload = type === 'upload';
    const subject = isUpload ? 'fMRI Data Upload Confirmation' : 'fMRI Data Deletion Confirmation';
    const actionText = isUpload ? 'uploaded' : 'deleted';
    const actionColor = isUpload ? '#10b981' : '#ef4444';

    const filesList = files.map(file => `<li style="margin: 4px 0; color: #374151;">${file}</li>`).join('');

    const html = `
      <!DOCTYPE html>
      <html>
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>${subject}</title>
        </head>
        <body style="margin: 0; padding: 0; background-color: #f9fafb; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;">
          <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1); overflow: hidden;">
            <!-- Header -->
            <div style="background: linear-gradient(135deg, #6366f1, #8b5cf6); padding: 32px 24px; text-align: center;">
              <div style="width: 48px; height: 48px; background-color: rgba(255, 255, 255, 0.2); border-radius: 12px; margin: 0 auto 16px; display: flex; align-items: center; justify-content: center;">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="color: white;">
                  <path d="M9.663 17h4.673M12 3v1m6.364 1.636-.707.707M21 12h-1M4 12H3m3.343-5.657-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386L8.464 15.536z"/>
                </svg>
              </div>
              <h1 style="color: white; font-size: 24px; font-weight: 600; margin: 0;">fMRI Portal</h1>
              <p style="color: rgba(255, 255, 255, 0.9); font-size: 16px; margin: 8px 0 0;">Neuroimaging Data Visualization Platform</p>
            </div>

            <!-- Content -->
            <div style="padding: 32px 24px;">
              <div style="text-align: center; margin-bottom: 32px;">
                <div style="width: 64px; height: 64px; background-color: ${actionColor}; border-radius: 50%; margin: 0 auto 16px; display: flex; align-items: center; justify-content: center;">
                  ${isUpload ? 
                    '<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2"><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M17 8l-5-5-5 5M12 3v12"/></svg>' :
                    '<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2"><path d="M3 6h18M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2M10 11v6M14 11v6"/></svg>'
                  }
                </div>
                <h2 style="color: #111827; font-size: 20px; font-weight: 600; margin: 0 0 8px;">Files ${actionText.charAt(0).toUpperCase() + actionText.slice(1)} Successfully</h2>
                <p style="color: #6b7280; font-size: 14px; margin: 0;">Hi ${userName}, your fMRI data has been ${actionText}.</p>
              </div>

              <div style="background-color: #f9fafb; border-radius: 8px; padding: 20px; margin-bottom: 24px;">
                <h3 style="color: #374151; font-size: 16px; font-weight: 600; margin: 0 0 12px;">File Details:</h3>
                <ul style="margin: 0; padding-left: 20px; list-style-type: disc;">
                  ${filesList}
                </ul>
                <p style="color: #6b7280; font-size: 14px; margin: 16px 0 0; padding-top: 16px; border-top: 1px solid #e5e7eb;">
                  <strong>Timestamp:</strong> ${new Date(timestamp).toLocaleString()}
                </p>
              </div>

              <div style="text-align: center;">
                <a href="${Deno.env.get('SUPABASE_URL')?.replace('.supabase.co', '') || 'https://your-app'}" 
                   style="display: inline-block; background: linear-gradient(135deg, #6366f1, #8b5cf6); color: white; text-decoration: none; padding: 12px 24px; border-radius: 6px; font-weight: 500; font-size: 14px;">
                  View Dashboard
                </a>
              </div>
            </div>

            <!-- Footer -->
            <div style="background-color: #f9fafb; padding: 24px; text-align: center; border-top: 1px solid #e5e7eb;">
              <p style="color: #6b7280; font-size: 12px; margin: 0;">
                This is an automated notification from fMRI Portal.<br>
                You can manage your email preferences in your dashboard settings.
              </p>
            </div>
          </div>
        </body>
      </html>
    `;

    const emailResponse = await resend.emails.send({
      from: "fMRI Portal <notifications@resend.dev>",
      to: [userEmail],
      subject: subject,
      html: html,
    });

    console.log("Email sent successfully:", emailResponse);

    return new Response(JSON.stringify({ success: true, emailId: emailResponse.data?.id }), {
      status: 200,
      headers: {
        "Content-Type": "application/json",
        ...corsHeaders,
      },
    });
  } catch (error: any) {
    console.error("Error in send-notification-email function:", error);
    return new Response(
      JSON.stringify({ error: error.message }),
      {
        status: 500,
        headers: { "Content-Type": "application/json", ...corsHeaders },
      }
    );
  }
};

serve(handler);