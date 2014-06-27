using System;

using Castle.MicroKernel;
using Castle.MicroKernel.Context;
using Castle.MicroKernel.Lifestyle.Scoped;

using OpenRasta.Pipeline;

namespace OpenRasta.DI.Windsor
{
    public class OpenRastaScopeAccessor : IScopeAccessor
    {
        private const string Key = "openrasta.per-web-request-lifestyle-cache";

        public void Dispose()
        {
            IKernel kernel = new DefaultKernel();
            var contextStore = kernel.Resolve<IContextStore>();
            
            if (contextStore == null)
                return;

            var lifetimeScope = (ILifetimeScope)contextStore[Key];

            if (lifetimeScope != null)
            {
                lifetimeScope.Dispose();
            }

            contextStore[Key] = null;
        }

        public ILifetimeScope GetScope(CreationContext context)
        {
            IKernel kernel = new DefaultKernel();

            IContextStore contextStore = null;

            if (kernel.HasComponent(typeof(IContextStore)))
            {
                contextStore = kernel.Resolve<IContextStore>();
            }

            if (contextStore == null)
            {
                throw new InvalidOperationException("IContextStore is null. A context store needs to be added to the container");
            }

            var lifetimeScope = (ILifetimeScope)contextStore[Key];

            if (lifetimeScope == null)
            {
                lifetimeScope = new OpenRastaLifetimeScope(new ScopeCache());
                contextStore[Key] = lifetimeScope;
            }

            return lifetimeScope;
        }
    }
}
