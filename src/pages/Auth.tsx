// pages/Auth.tsx
import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@/providers/AuthProvider";
import AuthGoogle from "@/components/AuthGoogle";

export default function Auth() {
  const { user, loading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!loading && user) {
      navigate("/dashboard", { replace: true }); // ⬅️ bounce to dashboard
    }
  }, [loading, user, navigate]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <div className="p-6 rounded-lg shadow-md bg-white space-y-4">
        <h1 className="text-xl font-semibold text-center">Sign in</h1>
        <p className="text-gray-500 text-sm text-center">
          Use your Google account to continue
        </p>
        <div className="flex flex-col items-center gap-3"><AuthGoogle /></div>
      </div>
    </div>
  );
}
