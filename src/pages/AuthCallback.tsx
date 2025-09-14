import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { supabase } from "@/integrations/supabase/client";

export default function AuthCallback() {
  const navigate = useNavigate();

  useEffect(() => {
    const handleAuthCallback = async () => {
      try {
        const { data, error } = await supabase.auth.getSession();
        
        if (error) {
          console.error("Auth callback error:", error);
          // If this is a popup, close it. Otherwise navigate to auth
          if (window.opener) {
            window.close();
          } else {
            navigate("/auth");
          }
          return;
        }

        if (data?.session?.user) {
          // If this is a popup, close it and let the parent handle navigation
          if (window.opener) {
            window.close();
          } else {
            navigate("/dashboard");
          }
        } else {
          // If this is a popup, close it. Otherwise navigate to auth
          if (window.opener) {
            window.close();
          } else {
            navigate("/auth");
          }
        }
      } catch (error) {
        console.error("Auth callback error:", error);
        // If this is a popup, close it. Otherwise navigate to auth
        if (window.opener) {
          window.close();
        } else {
          navigate("/auth");
        }
      }
    };

    handleAuthCallback();
  }, [navigate]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="text-center">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary mx-auto"></div>
        <p className="mt-2 text-muted-foreground">Completing authentication...</p>
      </div>
    </div>
  );
}