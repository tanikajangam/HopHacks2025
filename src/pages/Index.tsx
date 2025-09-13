import { useEffect } from "react";

const Index = () => {
  useEffect(() => {
    // Redirect to auth page for now
    window.location.href = "/auth";
  }, []);

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-background to-secondary/30">
      <div className="text-center">
        <h1 className="mb-4 text-4xl font-bold bg-gradient-to-r from-primary to-accent bg-clip-text text-transparent">
          fMRI Portal
        </h1>
        <p className="text-xl text-muted-foreground">Loading...</p>
      </div>
    </div>
  );
};

export default Index;
