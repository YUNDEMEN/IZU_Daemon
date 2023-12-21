using Autofac;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf.Builders;
using Topshelf.Configurators;
using Topshelf.HostConfigurators;
using Topshelf.ServiceConfigurators;

namespace izu.watcher.moxa
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class AutofacHostBuilderConfigurator : HostBuilderConfigurator
    {
        #region Static Fields

        private static ILifetimeScope lifetimeScope;

        #endregion

        #region Constructors and Destructors

        public AutofacHostBuilderConfigurator(ILifetimeScope lifetimeScope)
        {
            if (lifetimeScope == null)
            {
                throw new ArgumentNullException("lifetimeScope");
            }

            AutofacHostBuilderConfigurator.lifetimeScope = lifetimeScope;
        }

        #endregion

        #region Public Properties

        public static ILifetimeScope LifetimeScope
        {
            get
            {
                return lifetimeScope;
            }
        }

        #endregion

        #region Public Methods and Operators

        public HostBuilder Configure(HostBuilder builder)
        {
            return builder;
        }

        public IEnumerable<ValidateResult> Validate()
        {
            yield break;
        }

        private string GetDebuggerDisplay()
        {
            return ToString();
        }

        #endregion
    }

    public static class HostConfiguratorExtensions
    {
        #region Public Methods and Operators

        public static HostConfigurator UseAutofacContainer(this HostConfigurator configurator, ILifetimeScope lifetimeScope)
        {
            configurator.AddConfigurator(new AutofacHostBuilderConfigurator(lifetimeScope));
            return configurator;
        }

        #endregion
    }
    public static class ServiceConfiguratorExtensions
    {
        #region Public Methods and Operators

        public static ServiceConfigurator<T> ConstructUsingAutofacContainer<T>(this ServiceConfigurator<T> configurator) where T : class
        {
            configurator.ConstructUsing(serviceFactory => AutofacHostBuilderConfigurator.LifetimeScope.Resolve<T>());
            return configurator;
        }

        #endregion
    }
}
