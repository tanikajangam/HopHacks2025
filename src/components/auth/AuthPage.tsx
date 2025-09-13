import { useState } from "react";
import { LoginForm } from "./LoginForm";
import { SignupForm } from "./SignupForm";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";

export const AuthPage = () => {
  const [isLogin, setIsLogin] = useState(true);

  return (
    <div className="min-h-screen bg-gradient-to-br from-background to-secondary/30 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        {/* Header */}
        <div className="text-center mb-8">
          <h1 className="text-3xl font-bold bg-gradient-to-r from-primary to-accent bg-clip-text text-transparent">
            fMRI Portal
          </h1>
          <p className="text-muted-foreground mt-2">
            Secure neuroimaging data management platform
          </p>
        </div>

        {/* Auth Card */}
        <Card className="bg-card/50 backdrop-blur-sm border-border/50 shadow-lg">
          <CardContent className="p-6">
            {/* Toggle Buttons */}
            <div className="flex rounded-lg bg-secondary p-1 mb-6">
              <Button
                variant={isLogin ? "default" : "ghost"}
                className={`flex-1 ${isLogin ? "bg-primary shadow-sm" : ""}`}
                onClick={() => setIsLogin(true)}
              >
                Login
              </Button>
              <Button
                variant={!isLogin ? "default" : "ghost"}
                className={`flex-1 ${!isLogin ? "bg-primary shadow-sm" : ""}`}
                onClick={() => setIsLogin(false)}
              >
                Sign Up
              </Button>
            </div>

            {/* Forms */}
            {isLogin ? <LoginForm /> : <SignupForm />}
          </CardContent>
        </Card>

        {/* Footer */}
        <div className="text-center mt-6 text-sm text-muted-foreground">
          <p>Secure • HIPAA Compliant • Research Grade</p>
        </div>
      </div>
    </div>
  );
};