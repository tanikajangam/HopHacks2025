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
  Database
} from "lucide-react";
import { toast } from "@/components/ui/use-toast";
import { supabase } from "@/integrations/supabase/client";
import { useAuth } from "@/hooks/useAuth";

interface UploadFile {
  id: string;
  name: string;
  size: number;
  type: string;
  progress: number;
  status: 'uploading' | 'completed' | 'error';
}

export const UploadArea = () => {
  const [files, setFiles] = useState<UploadFile[]>([]);
  const [isDragOver, setIsDragOver] = useState(false);
  const { user } = useAuth();

  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const getFileIcon = (fileName: string) => {
    const extension = fileName.split('.').pop()?.toLowerCase();
    if (extension === 'nii' || extension === 'gz') {
      return <Brain className="h-5 w-5 text-primary" />;
    }
    if (extension === 'dcm' || extension === 'dicom') {
      return <FileImage className="h-5 w-5 text-accent" />;
    }
    return <File className="h-5 w-5 text-muted-foreground" />;
  };

  const uploadToSupabase = async (file: UploadFile, actualFile: File) => {
    if (!user) {
      toast({
        title: "Authentication Required",
        description: "Please log in to upload files.",
        variant: "destructive",
      });
      return;
    }

    const filePath = `${user.id}/${file.name}`;
    
    try {
      setFiles(prev => prev.map(f => 
        f.id === file.id ? { ...f, progress: 10 } : f
      ));

      const { error: uploadError } = await supabase.storage
        .from('fmri-data')
        .upload(filePath, actualFile, {
          cacheControl: '3600',
          upsert: false
        });

      if (uploadError) {
        throw uploadError;
      }

      setFiles(prev => prev.map(f => 
        f.id === file.id 
          ? { ...f, progress: 100, status: 'completed' } 
          : f
      ));

      toast({
        title: "Upload Completed",
        description: `${file.name} has been successfully uploaded to your fMRI data storage.`,
      });

    } catch (error) {
      console.error('Upload error:', error);
      setFiles(prev => prev.map(f => 
        f.id === file.id 
          ? { ...f, status: 'error' } 
          : f
      ));

      toast({
        title: "Upload Failed",
        description: error instanceof Error ? error.message : "Failed to upload file. Please try again.",
        variant: "destructive",
      });
    }
  };

  const handleFileSelect = (selectedFiles: FileList) => {
    if (!user) {
      toast({
        title: "Authentication Required",
        description: "Please log in to upload files.",
        variant: "destructive",
      });
      return;
    }

    const fileArray = Array.from(selectedFiles);
    const newFiles: UploadFile[] = fileArray.map(file => ({
      id: Math.random().toString(36).substr(2, 9),
      name: file.name,
      size: file.size,
      type: file.type,
      progress: 0,
      status: 'uploading' as const,
    }));

    setFiles(prev => [...prev, ...newFiles]);
    
    // Start real upload for each file
    newFiles.forEach((uploadFile, index) => {
      uploadToSupabase(uploadFile, fileArray[index]);
    });
  };

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
    
    const droppedFiles = e.dataTransfer.files;
    if (droppedFiles.length > 0) {
      handleFileSelect(droppedFiles);
    }
  }, []);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
  }, []);

  const removeFile = (id: string) => {
    setFiles(prev => prev.filter(f => f.id !== id));
  };

  return (
    <div className="space-y-6">
      {/* Drop Zone */}
      <div
        className={`border-2 border-dashed rounded-lg p-8 text-center transition-colors ${
          isDragOver 
            ? 'border-primary bg-primary/5' 
            : 'border-border hover:border-primary/50'
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
              const input = document.createElement('input');
              input.type = 'file';
              input.multiple = true;
              input.accept = '.nii,.nii.gz,.dcm,.dicom';
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

      {/* File List */}
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
                      {file.status === 'completed' && (
                        <CheckCircle className="h-5 w-5 text-green-500" />
                      )}
                      {file.status === 'error' && (
                        <AlertCircle className="h-5 w-5 text-destructive" />
                      )}
                      {file.status === 'uploading' && (
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
                
                {file.status === 'uploading' && (
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