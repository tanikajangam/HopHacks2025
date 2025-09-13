// Dashboard.tsx
import { useEffect, useState, useCallback } from "react";
import { useAuth } from "@/providers/AuthProvider";
import { supabase } from "@/lib/supabaseClient"; // for sign out button (optional)

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import {
  UploadCloud,
  Database,
  User as UserIcon,
  Activity,
  Settings,
  LogOut,
  Brain,
  FileText,
  Download,
  Copy,
} from "lucide-react";

// NEW: shadcn bits for popups & form controls
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { Avatar, AvatarImage, AvatarFallback } from "@/components/ui/avatar";
import { useToast } from "@/components/ui/use-toast";

import { UploadArea } from "./UploadArea";

type ActItem = {
  id: string;
  name: string; // full storage key: "<uid>/sub-.../func/filename"
  created_at: string; // ISO
};

function parseBidsMeta(name: string) {
  const lower = name.toLowerCase();
  const subject = (lower.match(/\bsub-[a-z0-9]+/i) || [])[0] ?? "unknown";
  const session = (lower.match(/\bses-[a-z0-9]+/i) || [])[0];
  const rules: Array<{ re: RegExp; modality: string }> = [
    { re: /(_task-|_bold|_timeseries|_sbref)/i, modality: "func" },
    { re: /(_t1w|_t2w|_flair|_pd|_t2star|_mprage|_spgr)/i, modality: "anat" },
    { re: /(_dwi|_adc|_fa)/i, modality: "dwi" },
    { re: /(_phasediff|_magnitude[12]?|_fieldmap|_epi)/i, modality: "fmap" },
    { re: /(_eeg|_ieeg|_meg)/i, modality: "eeg" },
  ];
  let modality = "misc";
  for (const r of rules)
    if (r.re.test(lower)) {
      modality = r.modality;
      break;
    }
  if (modality === "misc" && /\.h5$/.test(lower) && /(bold|timeseries)/i.test(lower))
    modality = "func";
  return { subject, session, modality };
}

function timeAgo(iso: string) {
  const s = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
  if (s < 60) return `${Math.floor(s)}s ago`;
  const m = s / 60;
  if (m < 60) return `${Math.floor(m)}m ago`;
  const h = m / 60;
  if (h < 24) return `${Math.floor(h)}h ago`;
  const d = h / 24;
  return `${Math.floor(d)}d ago`;
}

function getLastName(user: any): string {
  const m = user?.user_metadata ?? {};
  if (m.family_name) return m.family_name as string;
  if (m.last_name) return m.last_name as string;
  const full = (m.full_name as string) || (m.name as string) || "";
  if (full.trim()) {
    const cleaned = full.replace(/[.,]/g, " ").trim();
    let parts = cleaned.split(/\s+/);
    const suffixes = new Set(["jr", "sr", "ii", "iii", "iv", "phd", "md"]);
    const lastLower = parts[parts.length - 1]?.toLowerCase();
    if (suffixes.has(lastLower)) parts = parts.slice(0, -1);
    if (parts.length >= 1) return parts[parts.length - 1];
  }
  const emailPrefix = (user?.email as string | undefined)?.split("@")[0];
  return emailPrefix || "friend";
}

export const Dashboard = () => {
  const { user } = useAuth();
  const { toast } = useToast();

  const displayName =
    (user?.user_metadata?.full_name as string) ||
    (user?.user_metadata?.name as string) ||
    (user?.email?.split("@")[0] as string) ||
    "friend";

  const [activity, setActivity] = useState<ActItem[]>([]);
  const lastName = getLastName(user);

  const avatar =
    (user?.user_metadata?.avatar_url as string) ||
    (user?.user_metadata?.picture as string) ||
    "";

  // NEW: dialog state
  const [openSettings, setOpenSettings] = useState(false);
  const [openUser, setOpenUser] = useState(false);

  // NEW: simple settings state (client-only for now)
  const [theme, setTheme] = useState<"system" | "light" | "dark">("system");
  const [emailAlerts, setEmailAlerts] = useState(true);
  const [autoBidsSort, setAutoBidsSort] = useState(true);
  const [retentionDays, setRetentionDays] = useState(365);

  const handleSaveSettings = useCallback(() => {
    // Persist later if you add a table; for now just toast
    toast({
      title: "Settings saved",
      description: `Theme: ${theme}, Email alerts: ${emailAlerts ? "on" : "off"}, BIDS autosort: ${autoBidsSort ? "on" : "off"}, Retention: ${retentionDays} days`,
    });
    setOpenSettings(false);
  }, [theme, emailAlerts, autoBidsSort, retentionDays, toast]);

  const handleCopy = useCallback(async (text: string, label = "Copied") => {
    try {
      await navigator.clipboard.writeText(text);
      toast({ title: label });
    } catch {
      toast({ title: "Copy failed", variant: "destructive" });
    }
  }, [toast]);

  const handleSignOut = async () => {
    await supabase.auth.signOut();
    window.location.href = "/auth"; // optional
  };

  useEffect(() => {
    if (!user) return;
    const uid = user.id;

    supabase
      .schema("storage")
      .from("objects")
      .select("id,name,created_at")
      .eq("bucket_id", "fmri-data")
      .ilike("name", `${uid}/%`)
      .order("created_at", { ascending: false })
      .limit(15)
      .then(({ data, error }) => {
        if (error) {
          console.error("activity fetch error:", error);
          return;
        }
        setActivity(data ?? []);
      });

    const channel = supabase
      .channel("storage-activity")
      .on(
        "postgres_changes",
        {
          event: "INSERT",
          schema: "storage",
          table: "objects",
          filter: `bucket_id=eq.fmri-data`,
        },
        (payload: any) => {
          const row = payload.new as ActItem;
          if (!row?.name?.startsWith(`${uid}/`)) return;
          setActivity((prev) => [row, ...prev].slice(0, 15));
        }
      )
      .subscribe();

    return () => {
      supabase.removeChannel(channel);
    };
  }, [user]);

  return (
    <div className="min-h-screen bg-gradient-to-br from-background to-secondary/30">
      {/* Header */}
      <header className="bg-card/50 backdrop-blur-sm border-b border-border/50 px-6 py-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-primary/10 rounded-lg">
              <Brain className="h-6 w-6 text-primary" />
            </div>
            <div>
              <h1 className="text-xl font-semibold">fMRI Portal</h1>
              <p className="text-sm text-muted-foreground">
                Neuroimaging Data Management
              </p>
            </div>
          </div>

          <div className="flex items-center gap-4">
            <Badge
              variant="secondary"
              className="bg-accent/20 text-accent-foreground"
            >
              <Activity className="h-3 w-3 mr-1" />
              Online
            </Badge>

            <div className="flex items-center gap-2">
              <span className="text-sm">{displayName}</span>

              {/* Settings Button */}
              <Button variant="ghost" size="sm" onClick={() => setOpenSettings(true)}>
                <Settings className="h-4 w-4" />
              </Button>

              {/* User Button */}
              <Button variant="ghost" size="sm" onClick={() => setOpenUser(true)}>
                <UserIcon className="h-4 w-4" />
              </Button>

              <Button variant="ghost" size="sm" onClick={handleSignOut}>
                <LogOut className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="p-6">
        <div className="max-w-7xl mx-auto">
          {/* Welcome Section */}
          <div className="mb-8">
            <h2 className="text-2xl font-bold mb-2">
              Welcome back, Dr. {lastName}
            </h2>
            <p className="text-muted-foreground">
              Manage your fMRI datasets and track your research progress.
            </p>
          </div>

          {/* Stats Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
            <Card className="bg-card/50 backdrop-blur-sm border-border/50">
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium">
                  Total Datasets
                </CardTitle>
                <Database className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">24</div>
                <p className="text-xs text-muted-foreground">
                  +3 from last month
                </p>
              </CardContent>
            </Card>

            <Card className="bg-card/50 backdrop-blur-sm border-border/50">
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium">
                  Storage Used
                </CardTitle>
                <UploadCloud className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">2.4 TB</div>
                <Progress value={65} className="mt-2" />
                <p className="text-xs text-muted-foreground">
                  65% of 4TB quota
                </p>
              </CardContent>
            </Card>

            <Card className="bg-card/50 backdrop-blur-sm border-border/50">
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium">
                  Active Studies
                </CardTitle>
                <Brain className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">5</div>
                <p className="text-xs text-muted-foreground">
                  2 pending review
                </p>
              </CardContent>
            </Card>

            <Card className="bg-card/50 backdrop-blur-sm border-border/50">
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium">
                  Compliance Score
                </CardTitle>
                <FileText className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-primary">98%</div>
                <p className="text-xs text-muted-foreground">BIDS compliant</p>
              </CardContent>
            </Card>
          </div>

          {/* Main Grid */}
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
            {/* Upload Section */}
            <div className="lg:col-span-2">
              <Card className="bg-card/50 backdrop-blur-sm border-border/50">
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <UploadCloud className="h-5 w-5" />
                    Upload fMRI Data
                  </CardTitle>
                  <CardDescription>
                    Upload your neuroimaging data in BIDS format. Supported
                    formats: .nii, .nii.gz, DICOM
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <UploadArea />
                </CardContent>
              </Card>
            </div>

            {/* Recent Activity */}
            <div>
              <Card className="bg-card/50 backdrop-blur-sm border-border/50">
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <Activity className="h-5 w-5" />
                    Recent Activity
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  {activity.length === 0 && (
                    <>
                      <div className="text-sm text-muted-foreground">
                        No recent activity yet.
                      </div>
                      <Separator />
                    </>
                  )}
                  {activity.slice(0, 6).map((row) => {
                    const file = row.name.split("/").pop() || row.name;
                    return (
                      <div key={row.id} className="flex items-start gap-3">
                        <div className="p-1 bg-primary/10 rounded">
                          <UploadCloud className="h-3 w-3 text-primary" />
                        </div>
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium">Dataset uploaded</p>
                          <p className="text-xs text-muted-foreground truncate">
                            {file}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            {timeAgo(row.created_at)}
                          </p>
                        </div>
                      </div>
                    );
                  })}
                </CardContent>
              </Card>
            </div>
          </div>
        </div>
      </main>

      {/* SETTINGS DIALOG */}
      <Dialog open={openSettings} onOpenChange={setOpenSettings}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Settings</DialogTitle>
            <DialogDescription>Customize your portal preferences.</DialogDescription>
          </DialogHeader>

          <div className="space-y-6">
            <div className="grid grid-cols-1 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="theme">Theme</Label>
                <Select value={theme} onValueChange={(v: any) => setTheme(v)}>
                  <SelectTrigger id="theme"><SelectValue placeholder="Select theme" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="system">System</SelectItem>
                    <SelectItem value="light">Light</SelectItem>
                    <SelectItem value="dark">Dark</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <Separator />

              <div className="flex items-center justify-between">
                <div>
                  <Label>Email alerts</Label>
                  <p className="text-xs text-muted-foreground">
                    Notify me when uploads finish or fail.
                  </p>
                </div>
                <Switch checked={emailAlerts} onCheckedChange={setEmailAlerts} />
              </div>

              <div className="flex items-center justify-between">
                <div>
                  <Label>BIDS autosort</Label>
                  <p className="text-xs text-muted-foreground">
                    Auto-place files into <code>sub-*/ses-*/modality</code>.
                  </p>
                </div>
                <Switch checked={autoBidsSort} onCheckedChange={setAutoBidsSort} />
              </div>

              <div className="grid gap-2">
                <Label htmlFor="retention">Data retention (days)</Label>
                <Input
                  id="retention"
                  type="number"
                  min={1}
                  value={retentionDays}
                  onChange={(e) => setRetentionDays(Number(e.target.value || 1))}
                />
                <p className="text-xs text-muted-foreground">
                  For lifecycle rules—does not delete anything yet.
                </p>
              </div>
            </div>
          </div>

          <DialogFooter className="gap-2">
            <Button variant="ghost" onClick={() => setOpenSettings(false)}>Cancel</Button>
            <Button onClick={handleSaveSettings}>Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* USER DIALOG */}
      <Dialog open={openUser} onOpenChange={setOpenUser}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>User</DialogTitle>
            <DialogDescription>Account details and session actions.</DialogDescription>
          </DialogHeader>

          <div className="flex items-center gap-4">
            <Avatar className="h-14 w-14">
              <AvatarImage src={avatar || undefined} alt={displayName} />
              <AvatarFallback>
                {(displayName?.[0] || "U").toUpperCase()}
              </AvatarFallback>
            </Avatar>
            <div className="min-w-0">
              <div className="font-medium truncate">{displayName}</div>
              <div className="text-sm text-muted-foreground truncate">
                {user?.email}
              </div>
            </div>
          </div>

          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <div className="text-sm text-muted-foreground">User ID</div>
              <div className="flex items-center gap-2">
                <code className="text-xs truncate max-w-[220px]">{user?.id}</code>
                <Button
                  size="icon"
                  variant="ghost"
                  onClick={() => user?.id && handleCopy(user.id, "User ID copied")}
                >
                  <Copy className="h-4 w-4" />
                </Button>
              </div>
            </div>

            {user?.created_at && (
              <div className="flex items-center justify-between">
                <div className="text-sm text-muted-foreground">Joined</div>
                <div className="text-sm">
                  {new Date(user.created_at).toLocaleDateString()}
                </div>
              </div>
            )}
          </div>

          <DialogFooter className="gap-2">
            <Button variant="secondary" onClick={() => setOpenUser(false)}>
              Close
            </Button>
            <Button variant="destructive" onClick={handleSignOut}>
              <LogOut className="h-4 w-4 mr-1" />
              Sign out
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
};
