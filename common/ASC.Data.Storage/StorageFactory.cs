/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


using System;
using System.Collections.Generic;
using System.Linq;
using ASC.Common.Caching;
using ASC.Core;
using ASC.Core.Common.Configuration;
using ASC.Data.Storage.Configuration;
using ASC.Data.Storage.DiscStorage;
using ASC.Security.Cryptography;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ASC.Data.Storage
{
    public class StorageFactoryListener
    {
        public StorageFactoryListener(ICacheNotify<DataStoreCacheItem> cache)
        {
            cache.Subscribe((r) => DataStoreCache.Remove(r.TenantId, r.Module), CacheNotifyAction.Remove);
        }
    }

    public class StorageFactoryConfig
    {
        public Configuration.Storage Section { get; }

        public StorageFactoryConfig(IServiceProvider serviceProvider)
        {
            Section = serviceProvider.GetService<Configuration.Storage>();
        }

        public IEnumerable<string> GetModuleList(string configpath, bool exceptDisabledMigration = false)
        {
            return Section.Module
                .Where(x => x.Visible)
                .Where(x => !exceptDisabledMigration || !x.DisableMigrate)
                .Select(x => x.Name);
        }

        public IEnumerable<string> GetDomainList(string configpath, string modulename)
        {
            if (Section == null)
            {
                throw new ArgumentException("config section not found");
            }
            return
                Section.Module
                    .Single(x => x.Name.Equals(modulename, StringComparison.OrdinalIgnoreCase))
                    .Domain
                    .Where(x => x.Visible)
                    .Select(x => x.Name);
        }
    }

    public static class StorageFactoryExtenstion
    {
        public static void InitializeHttpHandlers(this IEndpointRouteBuilder builder, string config = null)
        {
            //TODO:
            //if (!HostingEnvironment.IsHosted)
            //{
            //    throw new InvalidOperationException("Application not hosted.");
            //}

            var section = builder.ServiceProvider.GetService<Configuration.Storage>();
            var pathUtils = builder.ServiceProvider.GetService<PathUtils>();
            if (section != null)
            {
                //old scheme
                var discHandler = section.GetHandler("disc");
                if (discHandler != null && section.Module != null)
                {
                    var props = discHandler.Property != null ? discHandler.Property.ToDictionary(r => r.Name, r => r.Value) : new Dictionary<string, string>();
                    foreach (var m in section.Module.Where(m => m.Type == "disc"))
                    {
                        if (m.Path.Contains(Constants.STORAGE_ROOT_PARAM))
                            builder.RegisterDiscDataHandler(
                                pathUtils.ResolveVirtualPath(m.VirtualPath),
                                pathUtils.ResolvePhysicalPath(m.Path, props),
                                m.Public);

                        if (m.Domain != null)
                        {
                            foreach (var d in m.Domain.Where(d => (d.Type == "disc" || string.IsNullOrEmpty(d.Type)) && d.Path.Contains(Constants.STORAGE_ROOT_PARAM)))
                            {
                                builder.RegisterDiscDataHandler(
                                    pathUtils.ResolveVirtualPath(d.VirtualPath),
                                    pathUtils.ResolvePhysicalPath(d.Path, props));
                            }
                        }
                    }
                }

                //new scheme
                if (section.Module != null)
                {
                    foreach (var m in section.Module)
                    {
                        //todo: add path criterion
                        if (m.Type == "disc" || !m.Public || m.Path.Contains(Constants.STORAGE_ROOT_PARAM))
                            builder.RegisterStorageHandler(
                                m.Name,
                                string.Empty,
                                m.Public);

                        //todo: add path criterion
                        if (m.Domain != null)
                        {
                            foreach (var d in m.Domain.Where(d => d.Path.Contains(Constants.STORAGE_ROOT_PARAM)))
                            {
                                builder.RegisterStorageHandler(
                                    m.Name,
                                    d.Name,
                                    d.Public);
                            }
                        }
                    }
                }
            }
        }
    }


    public class StorageFactory
    {
        private const string DefaultTenantName = "default";

        public StorageFactoryListener StorageFactoryListener { get; }
        public StorageFactoryConfig StorageFactoryConfig { get; }
        public StorageSettings StorageSettings { get; }
        public TenantManager TenantManager { get; }
        public CoreBaseSettings CoreBaseSettings { get; }
        public PathUtils PathUtils { get; }
        public EmailValidationKeyProvider EmailValidationKeyProvider { get; }

        public StorageFactory(
            StorageFactoryListener storageFactoryListener,
            StorageFactoryConfig storageFactoryConfig,
            StorageSettings storageSettings,
            TenantManager tenantManager,
            CoreBaseSettings coreBaseSettings,
            PathUtils pathUtils,
            EmailValidationKeyProvider emailValidationKeyProvider)
        {
            StorageFactoryListener = storageFactoryListener;
            StorageFactoryConfig = storageFactoryConfig;
            StorageSettings = storageSettings;
            TenantManager = tenantManager;
            CoreBaseSettings = coreBaseSettings;
            PathUtils = pathUtils;
            EmailValidationKeyProvider = emailValidationKeyProvider;
        }

        public IDataStore GetStorage(string tenant, string module)
        {
            return GetStorage(string.Empty, tenant, module);
        }

        public IDataStore GetStorage(string configpath, string tenant, string module)
        {
            int.TryParse(tenant, out var tenantId);
            return GetStorage(configpath, tenant, module, new TennantQuotaController(tenantId, TenantManager));
        }

        public IDataStore GetStorage(string configpath, string tenant, string module, IQuotaController controller)
        {
            var tenantId = -2;
            if (string.IsNullOrEmpty(tenant))
            {
                tenant = DefaultTenantName;
            }
            else
            {
                tenantId = Convert.ToInt32(tenant);
            }

            //Make tennant path
            tenant = TennantPath.CreatePath(tenant);

            var store = DataStoreCache.Get(tenant, module);
            if (store == null)
            {
                var section = StorageFactoryConfig.Section;
                if (section == null)
                {
                    throw new InvalidOperationException("config section not found");
                }

                var settings = StorageSettings.LoadForTenant(tenantId);

                store = GetStoreAndCache(tenant, module, settings.DataStoreConsumer, controller);
            }
            return store;
        }

        public IDataStore GetStorageFromConsumer(string configpath, string tenant, string module, DataStoreConsumer consumer)
        {
            if (tenant == null) tenant = DefaultTenantName;

            //Make tennant path
            tenant = TennantPath.CreatePath(tenant);

            var section = StorageFactoryConfig.Section;
            if (section == null)
            {
                throw new InvalidOperationException("config section not found");
            }

            int.TryParse(tenant, out var tenantId);
            return GetDataStore(tenant, module, consumer, new TennantQuotaController(tenantId, TenantManager));
        }

        private IDataStore GetStoreAndCache(string tenant, string module, DataStoreConsumer consumer, IQuotaController controller)
        {
            var store = GetDataStore(tenant, module, consumer, controller);
            if (store != null)
            {
                DataStoreCache.Put(store, tenant, module);
            }
            return store;
        }

        private IDataStore GetDataStore(string tenant, string module, DataStoreConsumer consumer, IQuotaController controller)
        {
            var storage = StorageFactoryConfig.Section;
            var moduleElement = storage.GetModuleElement(module);
            if (moduleElement == null)
            {
                throw new ArgumentException("no such module", module);
            }

            var handler = storage.GetHandler(moduleElement.Type);
            Type instanceType;
            IDictionary<string, string> props;

            if (CoreBaseSettings.Standalone &&
                !moduleElement.DisableMigrate &&
                consumer.IsSet)
            {
                instanceType = consumer.HandlerType;
                props = consumer;
            }
            else
            {
                instanceType = Type.GetType(handler.Type, true);
                props = handler.Property.ToDictionary(r => r.Name, r => r.Value);
            }

            return ((IDataStore)Activator.CreateInstance(instanceType, TenantManager, PathUtils, EmailValidationKeyProvider))
                .Configure(tenant, handler, moduleElement, props)
                .SetQuotaController(moduleElement.Count ? controller : null
                /*don't count quota if specified on module*/);
        }
    }
}