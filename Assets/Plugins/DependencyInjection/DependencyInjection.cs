using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RamjetAnvil.Util;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RamjetAnvil.DependencyInjection {

    // TODO Remove overrideExisting bool, it's part of the injector
    public delegate void DependencyInjector<in TContext>(
        object subject, InjectionPoint injectionPoint, TContext context, bool overrideExisting);

    public static class DependencyInjection {

        private static readonly Func<Type, IList<InjectionPoint>> GetInjectionPoints =
            Memoization.Memoize<Type, IList<InjectionPoint>>(GetInjectionPointsInternal);

        private static IList<InjectionPoint> GetInjectionPointsInternal(Type type) {
            var injectionPoints = new List<InjectionPoint>();
            var properties = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.Instance | BindingFlags.SetProperty |
                                                BindingFlags.DeclaredOnly);
            for (int i = 0; i < properties.Length; i++) {
                var propertyInfo = properties[i];
                var dependencyAttributes = propertyInfo.GetCustomAttributes(typeof (Dependency), inherit: true);
                if (dependencyAttributes.Length > 0) {
                    var dependencyInfo = (Dependency) dependencyAttributes.First();
                    injectionPoints.Add(new InjectionPoint(dependencyInfo, new PropertyMember(propertyInfo)));
                }
            }
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++) {
                var fieldInfo = fields[i];
                var dependencyAttributes = fieldInfo.GetCustomAttributes(typeof (Dependency), inherit: true);
                if (dependencyAttributes.Length > 0) {
                    var dependencyInfo = (Dependency) dependencyAttributes.First();
                    injectionPoints.Add(new InjectionPoint(dependencyInfo, new FieldMember(fieldInfo)));
                }
            }

            return injectionPoints;
        }

        private static void InjectInternal(this InjectionPoint injectionPoint, object subject, DependencyReference dependency,
            bool overrideExisting) {

            var isNameMatch = injectionPoint.Info.Name == null ||
                              injectionPoint.Info.Name.Equals(dependency.Name);
            var isTypeMatch = injectionPoint.Injector.Type.IsInstanceOfType(dependency.Instance)
                && injectionPoint.Injector.Info.DeclaringType.IsInstanceOfType(subject);
            var currentValue = injectionPoint.Injector.GetValue(subject);
            bool isDependencyAlreadySet;
            if (currentValue is UnityEngine.Object) {
                isDependencyAlreadySet = (currentValue as UnityEngine.Object) != null;
            } else {
                isDependencyAlreadySet = currentValue != null;
            }

            if (isNameMatch && isTypeMatch && (!isDependencyAlreadySet || overrideExisting)) {
                injectionPoint.Injector.SetValue(subject, dependency.Instance);
            }
        }

        private static readonly List<MonoBehaviour> ComponentCache = new List<MonoBehaviour>();
        public static void Inject<TContext>(object subject, 
            DependencyInjector<TContext> inject, TContext context,
            bool overrideExisting) {

            if (subject == null) {
                throw new ArgumentNullException("Cannot inject on a null component/object");
            }

            if (subject is GameObject) {
                var gameObject = subject as GameObject;
#if UNITY_EDITOR
                if (IsPrefab(gameObject)) {
                    throw new ArgumentException("Injecting on Prefab '" + gameObject.name + "' is not allowed.");
                }
#endif

                ComponentCache.Clear();
                gameObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true, results: ComponentCache);
                //Debug.Log("Injection on " + gameObject.name + ", components " + ComponentCache.Join(","));
                for (int i = 0; i < ComponentCache.Count; i++) {
                    var component = ComponentCache[i];
                    //Debug.Log("injection into " + component);
                    Inject(component, inject, context, overrideExisting);
                }
            } else {
                // TODO Do this for all super types
                var injectionPoints = GetInjectionPoints(subject.GetType());
                foreach (var injectionPoint in injectionPoints) {

                    // Search dependency for each injection point and inject it
                    inject(subject, injectionPoint, context, overrideExisting);
                }
            }
        }

        public static void InjectSingle(object subject, object dependency, bool overrideExisting = false) {
            Inject(subject, InjectSingleDependency, dependency, overrideExisting);
        }

        public static void Inject(object subject, DependencyContainer diContainer, bool overrideExisting = false) {
            Inject(subject, InjectFromContainer, diContainer, overrideExisting);
        }

        public static readonly DependencyInjector<object> InjectSingleDependency =
            (subject, injectionPoint, dependency, overrideExisting) => {
                if (injectionPoint.Injector.Type.IsInstanceOfType(dependency)) {
                    injectionPoint.InjectInternal(subject, new DependencyReference(name: null, instance: dependency), overrideExisting);
                }
            };

        public static readonly DependencyInjector<DependencyContainer> InjectFromContainer =
            (subject, injectionPoint, diContainer, overrideExisting) => {
                IList<DependencyReference> candidates;
                if (diContainer.DepsByType.TryGetValue(injectionPoint.Injector.Type, out candidates)) {
                    for (int i = 0; i < candidates.Count; i++) {
                        var dependency = candidates[i];
                        injectionPoint.InjectInternal(subject, dependency, overrideExisting);
                    }
                }
            };

#if UNITY_EDITOR
        private static bool IsPrefab(GameObject go) {
            return PrefabUtility.GetCorrespondingObjectFromSource(go) == null && PrefabUtility.GetPrefabObject(go) != null; // Is a prefab
        }
#endif
    }
}
