using System;

using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Lifestyle.Scoped;

namespace OpenRasta.DI.Windsor
{
    public class OpenRastaLifetimeScope : ILifetimeScope
    {
        private static readonly Action<Burden> emptyOnAfterCreated = delegate { };
        private readonly object @lock = new object();
        private readonly Action<Burden> onAfterCreated;
        private IScopeCache scopeCache;

        public OpenRastaLifetimeScope(IScopeCache scopeCache = null, Action<Burden> onAfterCreated = null)
        {
            this.scopeCache = scopeCache ?? new ScopeCache();
            this.onAfterCreated = onAfterCreated ?? emptyOnAfterCreated;
        }

        public void Dispose()
        {
            lock (@lock)
            {
                if (scopeCache == null)
                {
                    return;
                }
                var disposableCache = scopeCache as IDisposable;
                if (disposableCache != null)
                {
                    disposableCache.Dispose();
                }
                scopeCache = null;
            }
        }

        public Burden GetCachedInstance(ComponentModel model, ScopedInstanceActivationCallback createInstance)
        {
            lock (@lock)
            {
                Burden burden = scopeCache[model];
                if (burden == null)
                {
                    scopeCache[model] = burden = createInstance(onAfterCreated);
                }
                return burden;
            }
        }
    }
}