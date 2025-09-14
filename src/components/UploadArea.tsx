import { useState, useCallback } from "react";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import {
  Upload,
  File,
  CheckCircle,
  AlertCircle,
  X,
  Brain,
  FileImage,
  Database,
} from "lucide-react";
import { useToast } from "@/hooks/use-toast";
import { supabase } from "@/integrations/supabase/client";
import { useAuth } from "@/providers/AuthProvider";
import { sendEmailNotification } from "@/utils/emailNotifications";

interface UploadFile {
  id: string;
  name: string;
  size: number;
  type: string;
  progress: number;
  status: "uploading" | "completed" | "error";
}

export const UploadArea = ({ emailAlertsEnabled = false }: { emailAlertsEnabled?: boolean }) => {
  const [files, setFiles] = useState<UploadFile[]>([]);
  const [isDragOver, setIsDragOver] = useState(false);
  const { user } = useAuth();
  const { toast } = useToast();

  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return "0 Bytes";
    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  };

  const getFileIcon = (fileName: string) => {
    const extension = fileName.split(".").pop()?.toLowerCase();
    if (extension === "nii" || extension === "gz") {
      return <Brain className="h-5 w-5 text-primary" />;
    }
    if (extension === "dcm" || extension === "dicom") {
      return <FileImage className="h-5 w-5 text-accent" />;
    }
    return <File className="h-5 w-5 text-muted-foreground" />;
  };

  function bidsPathFromName(name: string): {
    subject: string;
    session?: string;
    modality: string;
  } {
    const base = name.toLowerCase();

    const subject = (base.match(/\bsub-[a-z0-9]+/i) || [])[0];
    if (!subject)
      throw new Error("Filename missing BIDS subject (e.g., sub-10159).");

    const session = (base.match(/\bses-[a-z0-9]+/i) || [])[0];

    const rules: Array<{ re: RegExp; modality: string }> = [
      { re: /(_task-|_bold|_timeseries|_sbref)/i, modality: "func" },
      { re: /(_t1w|_t2w|_flair|_pd|_t2star|_mprage|_spgr)/i, modality: "anat" },
      { re: /(_dwi|_adc|_fa)/i, modality: "dwi" },
      { re: /(_phasediff|_magnitude[12]?|_fieldmap|_epi)/i, modality: "fmap" },
      { re: /(_eeg|_ieeg|_meg)/i, modality: "eeg" },
    ];

    let modality = "misc";
    for (const r of rules) {
      if (r.re.test(base)) {
        modality = r.modality;
        break;
      }
    }

    if (modality === "misc" && /\.h5$/.test(base) && /(bold|timeseries)/i.test(base)) {
      modality = "func";
    }

    return { subject, session, modality };
  }

  const uploadToSupabase = async (file: UploadFile, actualFile: File) => {
    const { data: sessData, error: sessErr } = await supabase.auth.getSession();
    const uid = user?.id ?? sessData?.session?.user?.id;

    if (sessErr) {
      console.error("getSession error:", sessErr);
    }

    if (!uid) {
      toast({
        title: "Authentication Required",
        description: "Please log in to upload files.",
        variant: "destructive",
      });
      return;
    }

    let key: string;
    try {
      const { subject, session, modality } = bidsPathFromName(file.name);
      const dir = session ? `${subject}/${session}/${modality}` : `${subject}/${modality}`;
      key = `${uid}/${dir}/${file.name}`;
    } catch {
      key = `${uid}/misc/${file.name}`;
    }

    try {
      setFiles((prev) =>
        prev.map((f) => (f.id === file.id ? { ...f, progress: 10 } : f))
      );

      const { error: uploadError } = await supabase.storage
        .from("fmri-data")
        .upload(key, actualFile, { cacheControl: "3600", upsert: true });

      if (uploadError) throw uploadError;

      setFiles((prev) =>
        prev.map((f) =>
          f.id === file.id ? { ...f, progress: 100, status: "completed" } : f
        )
      );

      toast({
        title: "Upload Completed",
        description: `${file.name} uploaded successfully`,
      });

      // Send email notification if enabled
      if (emailAlertsEnabled && user?.email) {
        const userName = user.user_metadata?.full_name || user.email.split('@')[0];
        await sendEmailNotification({
          userEmail: user.email,
          userName,
          type: 'upload',
          files: [file.name]
        });
      }
    } catch (error) {
      console.error("Upload error:", error);
      setFiles((prev) =>
        prev.map((f) => (f.id === file.id ? { ...f, status: "error" } : f))
      );
      toast({
        title: "Upload Failed",
        description:
          error instanceof Error
            ? error.message
            : "Failed to upload. Check filename or policies.",
        variant: "destructive",
      });
    }
  };

  const handleFileSelect = useCallback((selectedFiles: FileList) => {
    const fileArray = Array.from(selectedFiles);
    const newFiles: UploadFile[] = fileArray.map((file) => ({
      id: Math.random().toString(36).substr(2, 9),
      name: file.name,
      size: file.size,
      type: file.type,
      progress: 0,
      status: "uploading" as const,
    }));

    setFiles((prev) => [...prev, ...newFiles]);

    newFiles.forEach((uploadFile, index) => {
      uploadToSupabase(uploadFile, fileArray[index]);
    });
  }, []);

  const handleDrop = useCallback((e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragOver(false);
    const droppedFiles = e.dataTransfer.files;
    if (droppedFiles.length > 0) {
      handleFileSelect(droppedFiles);
    }
  }, [handleFileSelect]);

  const handleDragOver = useCallback((e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    if (e.dataTransfer) e.dataTransfer.dropEffect = "copy";
    setIsDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragOver(false);
  }, []);

  const removeFile = (id: string) => {
    setFiles((prev) => prev.filter((f) => f.id !== id));
  };

  return (
    <div className="space-y-6">
      <div
        className={`border-2 border-dashed rounded-lg p-8 text-center transition-colors ${
          isDragOver
            ? "border-primary bg-primary/5"
            : "border-border hover:border-primary/50"
        }`}
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
      >
        <div className="flex flex-col items-center gap-4">
          <div className="p-3 bg-primary/10 rounded-full">
            <Upload className="h-8 w-8 text-primary" />
          </div>

          <div>
            <h3 className="text-lg font-semibold">Drop your fMRI files here</h3>
            <p className="text-muted-foreground mt-1">
              or click to browse your computer
            </p>
          </div>

          <div className="flex gap-2 flex-wrap justify-center">
            <Badge variant="secondary">.nii</Badge>
            <Badge variant="secondary">.nii.gz</Badge>
            <Badge variant="secondary">DICOM</Badge>
            <Badge variant="secondary">BIDS format</Badge>
          </div>

          <Button
            onClick={() => {
              const input = document.createElement("input");
              input.type = "file";
              input.multiple = true;
              input.accept = [
                ".nii",
                ".nii.gz",
                ".gz",
                ".dcm",
                ".dicom",
                ".h5",
                ".hdf5",
                "application/gzip",
                "application/x-hdf5",
                "application/octet-stream",
              ].join(",");
              input.onchange = (e) => {
                const target = e.target as HTMLInputElement;
                if (target.files) {
                  handleFileSelect(target.files);
                }
              };
              input.click();
            }}
            className="bg-primary hover:bg-primary/90"
          >
            <Upload className="h-4 w-4 mr-2" />
            Choose Files
          </Button>
        </div>
      </div>

      {files.length > 0 && (
        <div className="space-y-3">
          <h4 className="font-semibold flex items-center gap-2">
            <Database className="h-4 w-4" />
            Upload Progress
          </h4>

          {files.map((file) => (
            <Card key={file.id} className="bg-background/50">
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3 flex-1 min-w-0">
                    {getFileIcon(file.name)}

                    <div className="flex-1 min-w-0">
                      <p className="font-medium truncate">{file.name}</p>
                      <p className="text-sm text-muted-foreground">
                        {formatFileSize(file.size)}
                      </p>
                    </div>

                    <div className="flex items-center gap-2">
                      {file.status === "completed" && (
                        <CheckCircle className="h-5 w-5 text-green-500" />
                      )}
                      {file.status === "error" && (
                        <AlertCircle className="h-5 w-5 text-destructive" />
                      )}
                      {file.status === "uploading" && (
                        <div className="w-16">
                          <Progress value={file.progress} className="h-2" />
                        </div>
                      )}

                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => removeFile(file.id)}
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                </div>

                {file.status === "uploading" && (
                  <div className="mt-2">
                    <Progress value={file.progress} className="h-1" />
                    <p className="text-xs text-muted-foreground mt-1">
                      {Math.round(file.progress)}% uploaded
                    </p>
                  </div>
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
};