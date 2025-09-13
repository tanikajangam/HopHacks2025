import { supabase } from "@/lib/supabaseClient";
import { useAuth } from "@/providers/AuthProvider";
import { Button } from "@/components/ui/button";

export default function AuthGoogle() {
  const { user, loading } = useAuth();

  const signIn = async () => {
    const { error } = await supabase.auth.signInWithOAuth({
      provider: "google",
      options: {
        redirectTo: `${window.location.origin}/dashboard`, // return here after Google
        queryParams: { prompt: "select_account" }, // optional: force account chooser
      },
    });
    if (error) console.error("Google sign-in error:", error.message);
  };

  const signOut = async () => {
    const { error } = await supabase.auth.signOut();
    if (error) console.error("Sign-out error:", error.message);
  };

  

  return user ? (
    <div className="flex items-center gap-3">
      <img
        src={user.user_metadata?.avatar_url ?? ""}
        className="h-8 w-8 rounded-full ring-1 ring-black/10"
        alt=""
      />
      <span className="text-sm">{user.user_metadata?.name ?? user.email}</span>
      <Button onClick={signOut} className="border rounded px-3 py-1 text-sm">
        Sign out
      </Button>
    </div>
  ) : (
    <Button onClick={signIn} className="border rounded px-3 py-1 text-sm">
      {loading ? "Sign in with Google (loading…)" : "Sign in with Google"}
    </Button>
  );
}
