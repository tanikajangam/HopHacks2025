import { Outlet, Navigate, useLocation } from "react-router-dom";
import { useAuth } from "@/providers/AuthProvider";

export default function Protected() {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) return <div className="p-6">loading…</div>;

  if (!user) {
    // preserve where user wanted to go
    return <Navigate to="/auth" replace state={{ from: location }} />;
  }

  return <Outlet />; // render the nested protected routes
}
