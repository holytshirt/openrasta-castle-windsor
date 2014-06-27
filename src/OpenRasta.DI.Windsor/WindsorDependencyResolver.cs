#region License

/* Authors:
 *      Sebastien Lambla (seb@serialseb.com)
 * Copyright:
 *      (C) 2007-2009 Caffeine IT & naughtyProd Ltd (http://www.caffeine-it.com)
 * License:
 *      This file is distributed under the terms of the MIT License found at the end of this file.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using OpenRasta.DI.Internal;
using OpenRasta.Pipeline;

namespace OpenRasta.DI.Windsor
{
    public class WindsorDependencyResolver : DependencyResolverCore, IDependencyResolver
    {
        readonly IWindsorContainer _windsorContainer;
        static readonly object ContainerLock = new object();

        public WindsorDependencyResolver(IWindsorContainer container)
        {
            _windsorContainer = container;
        }

        public bool HasDependency(Type serviceType)
        {
            if (serviceType == null) return false;
            return AvailableHandlers(_windsorContainer.Kernel.GetHandlers(serviceType)).Any();
        }

        public bool HasDependencyImplementation(Type serviceType, Type concreteType)
        {
            return
                AvailableHandlers(_windsorContainer.Kernel.GetHandlers(serviceType))
                    .Any(h => h.ComponentModel.Implementation == concreteType);
        }

        public void HandleIncomingRequestProcessed()
        {
            var store = _windsorContainer.Resolve<IContextStore>();

            store.Destruct();
        }

        protected override object ResolveCore(Type serviceType)
        {
            var handler = _windsorContainer.Kernel.GetHandler(serviceType);

            if (handler.ComponentModel.ExtendedProperties[Constants.REG_IS_INSTANCE_KEY] != null)
            {
                throw new DependencyResolutionException("Cannot find the instance in the context store.");
            }



            var resolveCore = _windsorContainer.Resolve(serviceType);

            return resolveCore;
        }

        protected override IEnumerable<TService> ResolveAllCore<TService>()
        {
            var handlers = _windsorContainer.Kernel.GetAssignableHandlers(typeof (TService));
            var resolved = new List<TService>();
            foreach (var handler in AvailableHandlers(handlers))
                try
                {
                    resolved.Add( _windsorContainer.Resolve<TService>(handler.ComponentModel.Name));
                }
                catch
                {
                    continue;
                }
            return resolved;
        }

        protected override void AddDependencyCore(Type dependent, Type concrete, DependencyLifetime lifetime)
        {
            string componentName = Guid.NewGuid().ToString();
            lock (ContainerLock)
            {
                if (lifetime != DependencyLifetime.PerRequest)
                {
                    _windsorContainer.Register(Component.For(dependent).ImplementedBy(concrete).Named(componentName).LifeStyle.Is(ConvertLifestyles.ToLifestyleType(lifetime)));
                }
                else
                {
                   // _windsorContainer.Register(Component.For(dependent).Named(componentName).ImplementedBy(concrete).LifeStyle.Custom(typeof(ContextStoreLifetime)));
                    _windsorContainer.Register(Component.For(dependent).Named(componentName).ImplementedBy(concrete).LifestyleScoped<OpenRastaScopeAccessor>());
                }
            }
        }

        protected override void AddDependencyInstanceCore(Type serviceType, object instance, DependencyLifetime lifetime)
        {
            string key = Guid.NewGuid().ToString();

            switch (lifetime)
            {
                case DependencyLifetime.PerRequest:
                {
                    if (_windsorContainer.Kernel.HasComponent(serviceType))
                    {
                        var handler = _windsorContainer.Kernel.GetHandler(serviceType);
                        if (handler.ComponentModel.ExtendedProperties[Constants.REG_IS_INSTANCE_KEY] != null)
                        {
                            _windsorContainer.Register(Component.For(serviceType).Instance(instance).Named(key).LifestyleScoped<OpenRastaScopeAccessor>().ExtendedProperties(new Property(Constants.REG_IS_INSTANCE_KEY, true)));
                        }
                        else
                        {
                            throw new DependencyResolutionException("Cannot register an instance for a type already registered");
                        }
                    }
                    else
                    {
                        lock (ContainerLock)
                        {
                            if (_windsorContainer.Kernel.HasComponent(serviceType) == false)
                            {
                                _windsorContainer.Register(Component.For(serviceType).Instance(instance).Named(key).LifestyleScoped<OpenRastaScopeAccessor>().ExtendedProperties(new Property(Constants.REG_IS_INSTANCE_KEY, true)));
                            }
                        }
                        
                    }


                    //var store = (IContextStore)Resolve(typeof(IContextStore));
                    //// try to see if we have a registration already
                    //if (_windsorContainer.Kernel.HasComponent(serviceType))
                    //{
                    //    var handler = _windsorContainer.Kernel.GetHandler(serviceType);
                    //    if (handler.ComponentModel.ExtendedProperties[Constants.REG_IS_INSTANCE_KEY] != null)
                    //    {
                    //        // if there's already an instance registration we update the store with the correct reg.
                    //        store[handler.ComponentModel.Name] = instance;
                    //    }
                    //    else
                    //    {
                    //        throw new DependencyResolutionException("Cannot register an instance for a type already registered");
                    //    }
                    //}
                    //else
                    //{
                    //    lock (ContainerLock)
                    //    {
                    //        if (_windsorContainer.Kernel.HasComponent(serviceType) == false)
                    //        {
                    //            _windsorContainer.Register(
                    //                Component.For(serviceType)
                    //                         .Activator<ContextStoreInstanceActivator>()
                    //                         .LifestyleCustom<ContextStoreLifetime>()
                    //                         .ImplementedBy(instance.GetType())
                    //                         .Named(key)
                    //                         .ExtendedProperties(new Property(Constants.REG_IS_INSTANCE_KEY, true))
                    //                         );
                    //            store[key] = instance;
                    //        }
                    //    }
                    //}
                }
                    break;
                case DependencyLifetime.Singleton:
                    lock (ContainerLock)
                    {
                        _windsorContainer.Register(Component.For(serviceType).Instance(instance).Named(key).LifeStyle.Singleton);
                    }
                    break;
            }
        }

        protected override void AddDependencyCore(Type handlerType, DependencyLifetime lifetime)
        {
            AddDependencyCore(handlerType, handlerType, lifetime);
        }

        IEnumerable<IHandler> AvailableHandlers(IEnumerable<IHandler> handlers)
        {
            return from handler in handlers
                   where handler.CurrentState == HandlerState.Valid
                         && IsAvailable(handler.ComponentModel)
                   select handler;
        }

        bool IsAvailable(ComponentModel component)
        {
            return _windsorContainer.Kernel.HasComponent(component.Name);

            //bool isWebInstance = IsWebInstance(component);
            //if (isWebInstance)
            //{
            //    if (component.Name == null || !HasDependency(typeof (IContextStore))) return false;
            //    var store = _windsorContainer.Resolve<IContextStore>();
            //    bool isInstanceAvailable = store[component.Name] != null;

            //    _windsorContainer.

            //    return isInstanceAvailable;
            //}
            //return true;
        }

        //static bool IsWebInstance(ComponentModel component)
        //{
        //    return typeof (ContextStoreLifetime).IsAssignableFrom(component.CustomLifestyle)
        //           && component.ExtendedProperties[Constants.REG_IS_INSTANCE_KEY] != null;
        //}
    }
}

#region Full license

// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion