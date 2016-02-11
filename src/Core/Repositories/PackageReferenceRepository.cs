using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace NuGet
{
    /// <summary>
    /// This repository implementation keeps track of packages that are referenced in a project but
    /// it also has a reference to the repository that actually contains the packages. It keeps track
    /// of packages in an xml file at the project root (packages.xml).
    /// </summary>
    public class PackageReferenceRepository : IPackageReferenceRepository, IPackageLookup, IPackageConstraintProvider, ILatestPackageLookup
    {
        private readonly PackageReferenceFile _packageReferenceFile;
        private readonly string _fullPath;

        public PackageReferenceRepository(
            IFileSystem fileSystem, 
            string projectName, 
            ISharedPackageRepository sourceRepository)
        {
            if (fileSystem == null)
            {
                throw new ArgumentNullException("fileSystem");
            }
            if (sourceRepository == null)
            {
                throw new ArgumentNullException("sourceRepository");
            }

            _packageReferenceFile = new PackageReferenceFile(
                fileSystem, Constants.PackageReferenceFile, projectName);

            _fullPath = _packageReferenceFile.FullPath;
            SourceRepository = sourceRepository;
        }

        public PackageReferenceRepository(
            string configFilePath,
            ISharedPackageRepository sourceRepository)
        {
            if (String.IsNullOrEmpty(configFilePath))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "configFilePath");
            }

            if (sourceRepository == null)
            {
                throw new ArgumentNullException("sourceRepository");
            }

            _packageReferenceFile = new PackageReferenceFile(configFilePath);
            _fullPath = configFilePath;
            SourceRepository = sourceRepository;
        }

        public string Source
        {
            get
            {
                return Constants.PackageReferenceFile;
            }
        }

        public PackageSaveModes PackageSaveMode
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public bool SupportsPrereleasePackages
        {
            get { return true; }
        }

        private ISharedPackageRepository SourceRepository
        {
            get;
            set;
        }

        private string PackageReferenceFileFullPath
        {
            get
            {
                return _fullPath;
            }
        }

        public PackageReferenceFile ReferenceFile
        {
            get
            {
                return _packageReferenceFile;
            }
        }

	    public bool AllowMissingPackages { get; set; }

	    public IQueryable<IPackage> GetPackages()
        {
            return GetPackagesCore().AsQueryable();
        }

        private IEnumerable<IPackage> GetPackagesCore()
        {
            return _packageReferenceFile.GetPackageReferences()
                                        .Select(GetPackage)
                                        .Where(p => p != null);
        }

        public void AddPackage(IPackage package)
        {
            AddPackage(package.Id, package.Version, package.DevelopmentDependency, targetFramework: null);
        }

        public void RemovePackage(IPackage package)
        {
            if (_packageReferenceFile.DeleteEntry(package.Id, package.Version))
            {
                // Remove the repository from the source
                SourceRepository.UnregisterRepository(PackageReferenceFileFullPath);
            }
        }

        public IPackage FindPackage(string packageId, SemanticVersion version)
        {
            if (!_packageReferenceFile.EntryExists(packageId, version))
            {
                return null;
            }

            return SourceRepository.FindPackage(packageId, version);
        }

        public IEnumerable<IPackage> FindPackagesById(string packageId)
        {
            return GetPackageReferences(packageId).Select(GetPackage)
                                                  .Where(p => p != null);
        }

        public bool Exists(string packageId, SemanticVersion version)
        {
            return _packageReferenceFile.EntryExists(packageId, version);
        }

        public void RegisterIfNecessary()
        {
            if (GetPackages().Any())
            {
                SourceRepository.RegisterRepository(PackageReferenceFileFullPath);
            }
        }

        public IVersionSpec GetConstraint(string packageId)
        {
            // Find the reference entry for this package
            var reference = GetPackageReference(packageId);
            if (reference != null)
            {
                return reference.VersionConstraint;
            }
            return null;
        }

        public bool TryFindLatestPackageById(string id, out SemanticVersion latestVersion)
        {
            PackageReference reference = GetPackageReferences(id).OrderByDescending(r => r.Version)
                                                                 .FirstOrDefault();
            if (reference == null)
            {
                latestVersion = null;
                return false;
            }
            else
            {
                latestVersion = reference.Version;
                Debug.Assert(latestVersion != null);
                return true;
            }
        }

        public bool TryFindLatestPackageById(string id, bool includePrerelease, out IPackage package)
        {
            IEnumerable<PackageReference> references = GetPackageReferences(id);
            if (!includePrerelease) 
            {
                references = references.Where(r => String.IsNullOrEmpty(r.Version.SpecialVersion));
            }

            PackageReference reference = references.OrderByDescending(r => r.Version).FirstOrDefault();
            if (reference != null)
            {
                package = GetPackage(reference);
                return true;
            }
            else
            {
                package = null;
                return false;
            }
        }

        public void AddPackage(string packageId, SemanticVersion version, bool developmentDependency, FrameworkName targetFramework)
        {
            _packageReferenceFile.AddEntry(packageId, version, developmentDependency, targetFramework);

            // Notify the source repository every time we add a new package to the repository.
            // This doesn't really need to happen on every package add, but this is over agressive
            // to combat scenarios where the 2 repositories get out of sync. If this repository is already 
            // registered in the source then this will be ignored
            SourceRepository.RegisterRepository(PackageReferenceFileFullPath);
        }

        public FrameworkName GetPackageTargetFramework(string packageId)
        {
            var reference = GetPackageReference(packageId);
            if (reference != null)
            {
                return reference.TargetFramework;
            }
            return null;
        }

        private PackageReference GetPackageReference(string packageId)
        {
            return GetPackageReferences(packageId).FirstOrDefault();
        }

        /// <summary>
        /// Gets all references to a specific package id that are valid.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        private IEnumerable<PackageReference> GetPackageReferences(string packageId)
        {
            return _packageReferenceFile.GetPackageReferences()
                                        .Where(reference => IsValidReference(reference) && 
                                                            reference.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        }

        private IPackage GetPackage(PackageReference reference)
        {
            if (IsValidReference(reference))
            {
	            IPackage findPackage = SourceRepository.FindPackage(reference.Id, reference.Version);
				if (findPackage == null && AllowMissingPackages)
	            {
		            findPackage=new MissingPackage(reference);
					
	            }
	            return findPackage;
            }
	        return null;
        }

        private static bool IsValidReference(PackageReference reference)
        {
            return !String.IsNullOrEmpty(reference.Id) && reference.Version != null;
        }

		public class MissingPackage:IPackage
		{
			private readonly PackageReference _reference;

			public MissingPackage(PackageReference reference)
			{
				_reference = reference;
			}

			public string Id { get { return _reference.Id; } }
			public SemanticVersion Version { get { return _reference.Version; } }
			public string Title { get { return "<missing package>"; } }
			public IEnumerable<string> Authors {get { return new String[0]; } }
			public IEnumerable<string> Owners { get { return new String[0]; } }
			public Uri IconUrl {get { return null; }}
			public Uri LicenseUrl { get { return null; } }
			public Uri ProjectUrl { get { return null; } }
			public bool RequireLicenseAcceptance { get { return false; }}
			public bool DevelopmentDependency { get { return _reference.IsDevelopmentDependency; } }
			public string Description { get { return "<missing package>"; } }
			public string Summary { get { return "<missing package>"; } }
			public string ReleaseNotes { get { return "<missing package>"; } }
			public string Language { get { return "<missing package>"; } }
			public string Tags { get { return "<missing package>"; } }
			public string Copyright { get { return "<missing package>"; } }
			public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies { get { return new FrameworkAssemblyReference[0]; } }
			public ICollection<PackageReferenceSet> PackageAssemblyReferences { get { return new PackageReferenceSet[0]; } }
			public IEnumerable<PackageDependencySet> DependencySets { get { return new PackageDependencySet[0]; } }
			public Version MinClientVersion {get {return new Version();} }
			public Uri ReportAbuseUrl { get { return null; } }
			public int DownloadCount { get { return 0; } }
			public bool IsAbsoluteLatestVersion { get { return false; }}
			public bool IsLatestVersion { get { return false; } }
			public bool Listed { get { return false; } }
			public DateTimeOffset? Published { get { return null; } }
			public IEnumerable<IPackageAssemblyReference> AssemblyReferences { get { return new IPackageAssemblyReference[0]; } }
			public IEnumerable<IPackageFile> GetFiles()
			{
				return new IPackageFile[0];
			}

			public IEnumerable<FrameworkName> GetSupportedFrameworks()
			{
				return new FrameworkName[0];
			}

			public Stream GetStream()
			{
				throw new NotSupportedException("Package is missing, can't GetStream");
			}

			public void ExtractContents(IFileSystem fileSystem, string extractPath)
			{
				throw new NotSupportedException("Package is missing, can't ExtractContents");
			}
		}
    }
}