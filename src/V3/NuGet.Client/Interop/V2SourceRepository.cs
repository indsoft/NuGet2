﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonLD.Core;
using Newtonsoft.Json.Linq;
using NuGet.Client.Diagnostics;

namespace NuGet.Client.Interop
{
    public class V2SourceRepository : SourceRepository
    {
        private readonly IPackageRepository _repository;
        private readonly LocalPackageRepository _lprepo;
        private readonly PackageSource _source;

        public override PackageSource Source { get { return _source; } }

        public V2SourceRepository(PackageSource source, IPackageRepository repository)
        {
            _source = source;

            _repository = repository;
            _lprepo = _repository as LocalPackageRepository;
        }

        public override Task<IEnumerable<JObject>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            NuGetTraceSources.V2SourceRepository.Verbose("search", "Searching for '{0}'", searchTerm);
            return Task.Factory.StartNew(() => _repository.Search(
                searchTerm, 
                filters.SupportedFrameworks.Select(fx => fx.FullName),
                filters.IncludePrerelease)
                .Skip(skip)
                .Take(take)
                .ToList()
                .Select(p => CreatePackageSearchResult(p)), cancellationToken);
        }

        private JObject CreatePackageSearchResult(IPackage package)
        {
            NuGetTraceSources.V2SourceRepository.Verbose("getallvers", "Retrieving all versions for {0}", package.Id);
            var versions = _repository.FindPackagesById(package.Id);

            string repoRoot = null;
            IPackagePathResolver resolver = null;
            if (_lprepo != null)
            {
                repoRoot = _lprepo.Source;
                resolver = _lprepo.PathResolver;
            }

            return PackageJsonLd.CreatePackageSearchResult(package, versions, repoRoot, resolver);
        }

        public override Task<JObject> GetPackageMetadata(string id, Versioning.NuGetVersion version)
        {
            NuGetTraceSources.V2SourceRepository.Verbose("getpackage", "Getting metadata for {0} {1}", id, version);
            var package = _repository.FindPackage(id, CoreConverters.SafeToSemVer(version));
            if (package == null)
            {
                return Task.FromResult<JObject>(null);
            }

            string repoRoot = null;
            IPackagePathResolver resolver = null;
            if (_lprepo != null)
            {
                repoRoot = _lprepo.Source;
                resolver = _lprepo.PathResolver;
            }

            return Task.FromResult(PackageJsonLd.CreatePackage(package, repoRoot, resolver));
        }

        public override Task<IEnumerable<JObject>> GetPackageMetadataById(string packageId)
        {
            NuGetTraceSources.V2SourceRepository.Verbose("findpackagebyid", "Getting metadata for all versions of {0}", packageId);
            string repoRoot = null;
            IPackagePathResolver resolver = null;
            if (_lprepo != null)
            {
                repoRoot = _lprepo.Source;
                resolver = _lprepo.PathResolver;
            }
            return Task.FromResult(_repository.FindPackagesById(packageId).Select(p => PackageJsonLd.CreatePackage(p, repoRoot, resolver)));
        }
    }
}
