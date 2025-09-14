import { useEffect } from "react";
import { useAuth } from "@/providers/AuthProvider";
import { useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Brain, Upload, Database, Shield } from "lucide-react";
import { supabase } from "@/integrations/supabase/client";
import { useToast } from "@/hooks/use-toast";

const Index = () => {
  const { user, loading } = useAuth();
  const navigate = useNavigate();
  const { toast } = useToast();

  useEffect(() => {
    if (user && !loading) {
      navigate("/dashboard");
    }
  }, [user, loading, navigate]);

  const signInWithGoogle = async () => {
    try {
      const { error } = await supabase.auth.signInWithOAuth({
        provider: "google",
        options: {
          redirectTo: window.location.origin + "/auth/callback",
        },
      });

      if (error) {
        throw error;
      }
    } catch (error: any) {
      toast({
        title: "Authentication failed",
        description: error.message || "Please try again",
        variant: "destructive",
      });
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <div className="text-center">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary mx-auto"></div>
          <p className="mt-2 text-muted-foreground">Loading...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-background via-primary/5 to-accent/10">
      <div className="container mx-auto px-4 py-16">
        <div className="grid lg:grid-cols-2 gap-12 items-center">
          {/* Left side - Welcome and features */}
          <div className="text-center lg:text-left">
            <div className="flex justify-center lg:justify-start mb-8">
              <div className="p-4 bg-gradient-to-br from-primary/20 to-accent/20 rounded-2xl backdrop-blur-sm">
                <Brain className="h-16 w-16 text-primary" />
              </div>
            </div>
            
            <h1 className="text-6xl font-bold mb-4">
              <span className="gradient-text">Welcome</span>
            </h1>
            <h2 className="text-4xl font-bold mb-6 text-foreground">
              to fMRI-VR
            </h2>
            
            <p className="text-xl text-muted-foreground mb-8 max-w-2xl">
              Secure, BIDS-compliant neuroimaging data management platform for researchers. 
              Upload, organize, and manage your fMRI datasets with ease.
            </p>

            <div className="grid gap-6 mb-8">
              <div className="flex items-center gap-4 p-4 rounded-lg bg-card/50 backdrop-blur-sm border">
                <Upload className="h-6 w-6 text-primary flex-shrink-0" />
                <div className="text-left">
                  <h3 className="font-semibold">Easy Upload</h3>
                  <p className="text-sm text-muted-foreground">
                    Drag and drop your neuroimaging files with automatic BIDS organization
                  </p>
                </div>
              </div>
              
              <div className="flex items-center gap-4 p-4 rounded-lg bg-card/50 backdrop-blur-sm border">
                <Database className="h-6 w-6 text-primary flex-shrink-0" />
                <div className="text-left">
                  <h3 className="font-semibold">Organized Storage</h3>
                  <p className="text-sm text-muted-foreground">
                    Automatic file organization following BIDS standards for reproducible research
                  </p>
                </div>
              </div>
              
              <div className="flex items-center gap-4 p-4 rounded-lg bg-card/50 backdrop-blur-sm border">
                <Shield className="h-6 w-6 text-primary flex-shrink-0" />
                <div className="text-left">
                  <h3 className="font-semibold">Secure Access</h3>
                  <p className="text-sm text-muted-foreground">
                    Google authentication with role-based access control for your research team
                  </p>
                </div>
              </div>
            </div>
          </div>

          {/* Right side - Auth card */}
          <div className="flex justify-center">
            <Card className="w-full max-w-md bg-card/80 backdrop-blur-sm border-2 border-primary/20 shadow-2xl">
              <CardHeader className="text-center space-y-4">
                <div className="mx-auto p-3 bg-gradient-to-br from-primary/20 to-accent/20 rounded-xl w-fit">
                  <Brain className="h-8 w-8 text-primary" />
                </div>
                <CardTitle className="text-2xl bg-gradient-to-r from-primary to-accent bg-clip-text text-transparent">
                  Get Started
                </CardTitle>
                <CardDescription className="text-base">
                  Sign in with your Google account to access the fMRI Portal
                </CardDescription>
              </CardHeader>
              <CardContent>
                <Button 
                  onClick={signInWithGoogle}
                  className="w-full h-12 text-lg bg-gradient-to-r from-primary to-accent hover:from-primary/90 hover:to-accent/90 transition-all duration-300"
                  disabled={loading}
                >
                  {loading ? (
                    <div className="flex items-center gap-2">
                      <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                      Signing in...
                    </div>
                  ) : (
                    "Continue with Google"
                  )}
                </Button>
              </CardContent>
            </Card>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Index;
