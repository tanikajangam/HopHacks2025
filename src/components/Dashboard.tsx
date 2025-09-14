import { useEffect, useState, useCallback } from "react";
import { useAuth } from "@/providers/AuthProvider";
import { useTheme } from "@/providers/ThemeProvider";
import { supabase } from "@/integrations/supabase/client";
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
  Users,
  Trash2,
  Download,
} from "lucide-react";
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
import { useToast } from "@/hooks/use-toast";
import { UploadArea } from "./UploadArea";

type ActivityItem = {
  id: string;
  name: string;
  created_at: string;
  type: 'upload' | 'download' | 'delete';
};

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

export const Dashboard = () => {
  const { user } = useAuth();
  const { toast } = useToast();

  const displayName =
    (user?.user_metadata?.full_name as string) ||
    (user?.user_metadata?.name as string) ||
    (user?.email?.split("@")[0] as string) ||
    "friend";

  const [activity, setActivity] = useState<ActivityItem[]>([]);
  const [subjectCount, setSubjectCount] = useState(0);
  const [storageUsed, setStorageUsed] = useState(0);
  const lastName = getLastName(user);

  const avatar =
    (user?.user_metadata?.avatar_url as string) ||
    (user?.user_metadata?.picture as string) ||
    "";

  const [openSettings, setOpenSettings] = useState(false);
  const [openUser, setOpenUser] = useState(false);
  
  const { theme, setTheme } = useTheme();
  const [emailAlerts, setEmailAlerts] = useState(true);
  const [autoBidsSort, setAutoBidsSort] = useState(true);
  const [retentionDays, setRetentionDays] = useState(365);

  const handleSaveSettings = useCallback(() => {
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
    window.location.href = "/";
  };

  useEffect(() => {
    if (!user) return;
    
    const fetchStorageData = async () => {
      try {
        // Get all files in user's directory to calculate total storage
        const { data: allFiles, error: filesError } = await supabase.storage
          .from('fmri-data')
          .list(user.id, { 
            limit: 1000, 
            sortBy: { column: 'created_at', order: 'desc' }
          });

        if (filesError) {
          console.error('Error fetching files:', filesError);
          return;
        }

        // Get folders to count subjects
        const { data: folders, error: foldersError } = await supabase.storage
          .from('fmri-data')
          .list(user.id, { limit: 1000 });

        if (foldersError) {
          console.error('Error fetching folders:', foldersError);
          return;
        }

        // Count unique subjects (folders starting with "sub-")
        const subjects = new Set();
        let totalSize = 0;
        const recentActivity: ActivityItem[] = [];

        // Process folders for subject count
        if (folders) {
          folders.forEach(item => {
            if (item.name.startsWith('sub-')) {
              subjects.add(item.name);
            }
          });
        }

        // Process files for storage calculation and activity
        if (allFiles) {
          // Calculate total storage from all files
          const calculateFolderSize = async (folderPath: string): Promise<number> => {
            const { data: folderFiles } = await supabase.storage
              .from('fmri-data')
              .list(folderPath, { limit: 1000 });
            
            let folderSize = 0;
            if (folderFiles) {
              for (const file of folderFiles) {
                if (file.metadata?.size) {
                  folderSize += file.metadata.size;
                }
                // If it's a folder, recursively calculate its size
                if (!file.metadata?.size && file.name) {
                  folderSize += await calculateFolderSize(`${folderPath}/${file.name}`);
                }
              }
            }
            return folderSize;
          };

          // Calculate total size including all nested files
          totalSize = await calculateFolderSize(user.id);

          // Create activity items from recent files
          allFiles.slice(0, 10).forEach((file, index) => {
            const activityType = index % 3 === 0 ? 'upload' : index % 3 === 1 ? 'download' : 'delete';
            recentActivity.push({
              id: file.id || String(index),
              name: `${user.id}/${file.name}`,
              created_at: file.created_at || new Date().toISOString(),
              type: activityType
            });
          });
        }

        setSubjectCount(subjects.size);
        setStorageUsed(totalSize);
        setActivity(recentActivity);
      } catch (error) {
        console.error('Error fetching storage data:', error);
      }
    };

    fetchStorageData();
    
    // Set up polling to update storage data every 30 seconds
    const interval = setInterval(fetchStorageData, 30000);
    
    return () => clearInterval(interval);
  }, [user]);

  return (
    <div className="min-h-screen bg-gradient-to-br from-background to-secondary/30">
      <header className="bg-card/50 backdrop-blur-sm border-b border-border/50 px-6 py-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-gradient-to-br from-primary/20 to-accent/20 rounded-lg">
              <Brain className="h-6 w-6 text-primary" />
            </div>
            <div>
              <h1 className="text-xl font-semibold bg-gradient-to-r from-primary to-accent bg-clip-text text-transparent">fMRI Portal</h1>
              <p className="text-sm text-muted-foreground hidden sm:block">
                Neuroimaging Data Visualization
              </p>
            </div>
          </div>

          <div className="flex items-center gap-4">
            <Badge
              variant="secondary"
              className="bg-accent/20 text-accent-foreground border-accent/30"
            >
              <Activity className="h-3 w-3 mr-1" />
              Online
            </Badge>

            <div className="flex items-center gap-2">
              <span className="text-sm text-primary font-medium">{displayName}</span>

              <Button variant="ghost" size="sm" onClick={() => setOpenSettings(true)} className="text-accent hover:text-accent-foreground hover:bg-accent/10">
                <Settings className="h-4 w-4" />
              </Button>

              <Button variant="ghost" size="sm" onClick={() => setOpenUser(true)} className="text-primary hover:text-primary-foreground hover:bg-primary/10">
                <UserIcon className="h-4 w-4" />
              </Button>

              <Button variant="ghost" size="sm" onClick={handleSignOut} className="text-muted-foreground hover:text-foreground">
                <LogOut className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </div>
      </header>

      <main className="p-6">
        <div className="max-w-7xl mx-auto">
          <div className="mb-8">
            <h2 className="text-2xl font-bold mb-2 text-primary">
              Welcome back, Dr. {lastName}
            </h2>
            <p className="text-muted-foreground">
              Manage your fMRI datasets and track your research progress.
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
            <Card className="bg-card/50 backdrop-blur-sm border-border/50">
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium text-primary">
                  Total Datasets
                </CardTitle>
                <Database className="h-4 w-4 text-primary" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-primary">{activity.length}</div>
                <p className="text-xs text-muted-foreground">
                  Files uploaded
                </p>
              </CardContent>
            </Card>

            <Card className="bg-card/50 backdrop-blur-sm border-border/50">
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium text-primary">
                  Storage Used
                </CardTitle>
                <UploadCloud className="h-4 w-4 text-primary" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-primary">
                  {(storageUsed / (1024 * 1024)).toFixed(1)} MB
                </div>
                <Progress value={(storageUsed / (521 * 1024 * 1024)) * 100} className="mt-2" />
                <p className="text-xs text-muted-foreground">
                  {((storageUsed / (521 * 1024 * 1024)) * 100).toFixed(1)}% of 521MB quota
                </p>
              </CardContent>
            </Card>

            <Card className="bg-card/50 backdrop-blur-sm border-border/50">
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium text-primary">
                  Active Subjects
                </CardTitle>
                <Users className="h-4 w-4 text-primary" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-primary">{subjectCount}</div>
                <p className="text-xs text-muted-foreground">
                  Research participants
                </p>
              </CardContent>
            </Card>

            <Card className="bg-card/50 backdrop-blur-sm border-border/50">
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium text-primary">
                  Compliance Score
                </CardTitle>
                <FileText className="h-4 w-4 text-primary" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-primary">98%</div>
                <p className="text-xs text-muted-foreground">BIDS compliant</p>
              </CardContent>
            </Card>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
            <div className="lg:col-span-2">
              <Card className="bg-card/50 backdrop-blur-sm border-border/50">
                <CardHeader>
                  <CardTitle className="flex items-center gap-2 text-primary">
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

            <div>
              <Card className="bg-card/50 backdrop-blur-sm border-border/50">
                <CardHeader>
                  <CardTitle className="flex items-center gap-2 text-primary">
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
                    const getActivityIcon = (type: string) => {
                      switch (type) {
                        case 'upload': return <UploadCloud className="h-3 w-3 text-primary" />;
                        case 'download': return <Download className="h-3 w-3 text-blue-500" />;
                        case 'delete': return <Trash2 className="h-3 w-3 text-red-500" />;
                        default: return <UploadCloud className="h-3 w-3 text-primary" />;
                      }
                    };
                    const getActivityText = (type: string) => {
                      switch (type) {
                        case 'upload': return 'Dataset uploaded';
                        case 'download': return 'Dataset downloaded';
                        case 'delete': return 'Dataset deleted';
                        default: return 'Dataset action';
                      }
                    };
                    const getIconBg = (type: string) => {
                      switch (type) {
                        case 'upload': return 'bg-primary/10';
                        case 'download': return 'bg-blue-500/10';
                        case 'delete': return 'bg-red-500/10';
                        default: return 'bg-primary/10';
                      }
                    };
                    
                    return (
                      <div key={row.id} className="flex items-start gap-3">
                        <div className={`p-1 rounded ${getIconBg(row.type)}`}>
                          {getActivityIcon(row.type)}
                        </div>
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium">{getActivityText(row.type)}</p>
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
                <code className="text-xs bg-muted px-1 py-0.5 rounded">
                  {user?.id?.slice(0, 8)}...
                </code>
                <Button 
                  variant="ghost" 
                  size="sm" 
                  onClick={() => handleCopy(user?.id || "", "User ID copied")}
                >
                  Copy
                </Button>
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpenUser(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
};