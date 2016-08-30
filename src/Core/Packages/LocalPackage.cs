using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace NuGet
{
    public abstract class LocalPackage : IPackage
    {
        private const string ResourceAssemblyExtension = ".resources.dll";
        private IList<IPackageAssemblyReference> _assemblyReferences;

        protected LocalPackage()
        {
            // local packages are typically listed; exception is with those served by NuGet.Server when delist feature is turned on
            Listed = true;
        }

        public string Id
        {
            get;
            set;
        }

        public SemanticVersion Version
        {
            get;
            set;
        }

        public string Title
        {
            get;
            set;
        }

        public IEnumerable<string> Authors
        {
            get;
            set;
        }

        public IEnumerable<string> Owners
        {
            get;
            set;
        }

        public Uri IconUrl
        {
            get;
            set;
        }

        public Uri LicenseUrl
        {
            get;
            set;
        }

        public Uri ProjectUrl
        {
            get;
            set;
        }

        public Uri ReportAbuseUrl
        {
            get
            {
                return null;
            }
        }

        public int DownloadCount
        {
            get
            {
                return -1;
            }
        }

        public bool RequireLicenseAcceptance
        {
            get;
            set;
        }

        public bool DevelopmentDependency
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public string Summary
        {
            get;
            set;
        }

        public string ReleaseNotes
        {
            get;
            set;
        }

        public string Language
        {
            get;
            set;
        }

        public string Tags
        {
            get;
            set;
        }

        public Version MinClientVersion
        {
            get;
            private set;
        }

        public bool IsAbsoluteLatestVersion
        {
            get
            {
                return true;
            }
        }

        public bool IsLatestVersion
        {
            get
            {
                return this.IsReleaseVersion();
            }
        }

        public bool Listed
        {
            get;
            set;
        }

        public DateTimeOffset? Published
        {
            get;
            set;
        }

        public string Copyright
        {
            get;
            set;
        }

        public IEnumerable<PackageDependencySet> DependencySets
        {
            get;
            set;
        }

        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies
        {
            get;
            set;
        }

        public IEnumerable<IPackageAssemblyReference> AssemblyReferences
        {
            get
            {
                if (_assemblyReferences == null)
                {
                    _assemblyReferences = GetAssemblyReferencesCore().ToList();
                }

                return _assemblyReferences;
            }
        }

        public ICollection<PackageReferenceSet> PackageAssemblyReferences
        {
            get;
            private set;
        }

        public virtual IEnumerable<FrameworkName> GetSupportedFrameworks()
        {
            return FrameworkAssemblies.SelectMany(f => f.SupportedFrameworks).Distinct();
        }

        public IEnumerable<IPackageFile> GetFiles()
        {
            return GetFilesBase();
        }

        public abstract Stream GetStream();

        public abstract void ExtractContents(IFileSystem fileSystem, string extractPath);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This operation can be expensive.")]
        protected abstract IEnumerable<IPackageFile> GetFilesBase();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This operation can be expensive.")]
        protected abstract IEnumerable<IPackageAssemblyReference> GetAssemblyReferencesCore();


        protected virtual string GetCacheFilePath()
        {
            return null;
        }

        protected virtual string GetPackagePath()
        {
            return null;
        }

        static readonly ConcurrentDictionary<string, object> FileLocks = new ConcurrentDictionary<string, object>();

        protected void ReadManifest(Stream manifestStream)
        {
            /*
            string packageFilePath = GetPackagePath();
	        Manifest manifest;
	        if (packageFilePath != null)
	        {
	            if (!File.Exists(packageFilePath)) File.Create(packageFilePath);
	            SafeFileHandle safeFileHandle = AlternateDataStreams.GetHandle(packageFilePath, "manifest");
	            object fileLock = FileLocks.GetOrAdd(packageFilePath, new object());
	            lock (fileLock)
	            {
	                using (FileStream fileStream = new FileStream(safeFileHandle, FileAccess.ReadWrite))
	                {
	                    if (fileStream.Length != 0)
	                    {
	                        manifest = Manifest.ReadFrom(fileStream, false, false);
	                    }
	                    else
	                    {
                            manifest = Manifest.ReadFrom(manifestStream, validateSchema: false);
	                        manifest.Save(fileStream, false);
	                    }

	                }
	            }
	        }
	        else
	        {
                manifest = Manifest.ReadFrom(manifestStream, validateSchema: false);               
	        }
            */


            string packageFilePath = GetPackagePath();
            Manifest manifest;
            // AlternateDataStreams.DeleteAlternateStream(packageFilePath, "manifest");
            if (AlternateDataStreams.AlternateStreamExist(packageFilePath, "manifest"))
            {

                object fileLock = FileLocks.GetOrAdd(packageFilePath, new object());
                lock (fileLock)
                {
                    SafeFileHandle safeFileHandle = AlternateDataStreams.GetHandle(packageFilePath, "manifest");
                    using (FileStream fs = new FileStream(safeFileHandle, FileAccess.Read))
                        manifest = Manifest.ReadFrom(fs, false, false);
                    safeFileHandle.Close();
                }
            }
            else
            {
                manifest = Manifest.ReadFrom(manifestStream, validateSchema: false);
                object fileLock = FileLocks.GetOrAdd(packageFilePath, new object());
                lock (fileLock)
                {
                    SafeFileHandle safeFileHandle = AlternateDataStreams.GetHandle(packageFilePath, "manifest");
                    using (FileStream fs = new FileStream(safeFileHandle, FileAccess.ReadWrite))
                        manifest.Save(fs, false);
                    safeFileHandle.Close();
                }
            }

            IPackageMetadata metadata = manifest.Metadata;

            Id = metadata.Id;
            Version = metadata.Version;
            Title = metadata.Title;
            Authors = metadata.Authors;
            Owners = metadata.Owners;
            IconUrl = metadata.IconUrl;
            LicenseUrl = metadata.LicenseUrl;
            ProjectUrl = metadata.ProjectUrl;
            RequireLicenseAcceptance = metadata.RequireLicenseAcceptance;
            DevelopmentDependency = metadata.DevelopmentDependency;
            Description = metadata.Description;
            Summary = metadata.Summary;
            ReleaseNotes = metadata.ReleaseNotes;
            Language = metadata.Language;
            Tags = metadata.Tags;
            DependencySets = metadata.DependencySets;
            FrameworkAssemblies = metadata.FrameworkAssemblies;
            Copyright = metadata.Copyright;
            PackageAssemblyReferences = metadata.PackageAssemblyReferences;
            MinClientVersion = metadata.MinClientVersion;

            // Ensure tags start and end with an empty " " so we can do contains filtering reliably
            if (!String.IsNullOrEmpty(Tags))
            {
                Tags = " " + Tags + " ";
            }
        }

        internal protected static bool IsAssemblyReference(string filePath)
        {
            // assembly reference must be under lib/
            if (!filePath.StartsWith(Constants.LibDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fileName = Path.GetFileName(filePath);

            // if it's an empty folder, yes
            if (fileName == Constants.PackageEmptyFileName)
            {
                return true;
            }

            // Assembly reference must have a .dll|.exe|.winmd extension and is not a resource assembly;
            return !filePath.EndsWith(ResourceAssemblyExtension, StringComparison.OrdinalIgnoreCase) &&
                Constants.AssemblyReferencesExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            // extension method, must have 'this'.
            return this.GetFullName();
        }
    }
}