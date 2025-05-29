using ReactiveUI;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows;

namespace VideoANPR
{
    // From https://stackoverflow.com/questions/30352447/using-reactiveuis-bindto-to-update-a-xaml-property-generates-a-warning
    public class CustomPropertyResolver : ICreatesObservableForProperty
    {
        public int GetAffinityForObject(Type type, string propertyName, bool beforeChanged = false)
        {
            if (!typeof(FrameworkElement).IsAssignableFrom(type))
                return 0;
            var fi = type.GetTypeInfo().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
              .FirstOrDefault(x => x.Name == propertyName);

            return fi != null ? 2 /* POCO affinity+1 */ : 0;
        }

        public IObservable<IObservedChange<object, object?>> GetNotificationForProperty(object sender, System.Linq.Expressions.Expression expression, string propertyName, bool beforeChanged = false, bool suppressWarnings = false)
        {
            return Observable.Never<IObservedChange<object, object?>>();
        }
    }
}
