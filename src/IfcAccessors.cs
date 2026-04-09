using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Bingosoft.Net.IfcMetadata
{
    internal static class IfcAccessors
    {
        private static readonly ConcurrentDictionary<Type, TypedIdStrategy> TypedIdStrategyCache = new();

        internal static IfcAccessorTelemetrySnapshot GetTelemetrySnapshot()
        {
            return IfcAccessorTelemetry.GetSnapshot();
        }

        internal static void ResetTelemetry()
        {
            IfcAccessorTelemetry.Reset();
        }

        internal static string GetTypedId(IIfcObjectDefinition element)
        {
            if (element is null)
            {
                return null;
            }

            if (element is IIfcObject ifcObject)
            {
                if (TryGetTypedIdFromTypedBy(ifcObject.IsTypedBy, out var interfaceTypedId))
                {
                    IfcAccessorTelemetry.TrackFast(AccessorKind.TypedId);
                    return interfaceTypedId;
                }

                IfcAccessorTelemetry.TrackFast(AccessorKind.TypedId);
                return null;
            }

            var runtimeType = element.GetType();
            var strategy = TypedIdStrategyCache.GetOrAdd(runtimeType, ResolveTypedIdStrategy);

            switch (strategy)
            {
                case TypedIdStrategy.AlwaysNull:
                    IfcAccessorTelemetry.TrackFast(AccessorKind.TypedId);
                    return null;
                case TypedIdStrategy.DirectTypedByHotIfc2x3:
                    if (TryGetTypedIdFromHotIfc2x3Types(element, out var hotTypedId))
                    {
                        IfcAccessorTelemetry.TrackFast(AccessorKind.TypedId);
                        return hotTypedId;
                    }

                    IfcAccessorTelemetry.TrackFast(AccessorKind.TypedId);
                    return null;
                default:
                    return TryGetTypedIdViaFallback(element, runtimeType, out var fallbackTypedId)
                        ? fallbackTypedId
                        : null;
            }
        }

        private static TypedIdStrategy ResolveTypedIdStrategy(Type runtimeType)
        {
            var fullName = runtimeType.FullName;
            return fullName switch
            {
                "Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey" => TypedIdStrategy.AlwaysNull,
                "Xbim.Ifc2x3.ProductExtension.IfcBuilding" => TypedIdStrategy.AlwaysNull,
                "Xbim.Ifc2x3.ProductExtension.IfcSite" => TypedIdStrategy.AlwaysNull,
                "Xbim.Ifc2x3.Kernel.IfcProject" => TypedIdStrategy.AlwaysNull,
                "Xbim.Ifc2x3.SharedBldgElements.IfcRoof" => TypedIdStrategy.DirectTypedByHotIfc2x3,
                "Xbim.Ifc2x3.SharedBldgElements.IfcRailing" => TypedIdStrategy.DirectTypedByHotIfc2x3,
                "Xbim.Ifc2x3.SharedBldgElements.IfcStair" => TypedIdStrategy.DirectTypedByHotIfc2x3,
                _ => TypedIdStrategy.FallbackDelegate,
            };
        }

        private static bool TryGetTypedIdFromHotIfc2x3Types(IIfcObjectDefinition element, out string typedId)
        {
            switch (element)
            {
                case Xbim.Ifc2x3.SharedBldgElements.IfcRoof roof:
                    return TryExtractGlobalId(roof.IsTypedBy, out typedId);
                case Xbim.Ifc2x3.SharedBldgElements.IfcRailing railing:
                    return TryExtractGlobalId(railing.IsTypedBy, out typedId);
                case Xbim.Ifc2x3.SharedBldgElements.IfcStair stair:
                    return TryExtractGlobalId(stair.IsTypedBy, out typedId);
                default:
                    typedId = null;
                    return false;
            }
        }

        private static bool TryGetTypedIdFromTypedBy(IEnumerable typedByRelations, out string typedId)
        {
            foreach (var relation in typedByRelations)
            {
                if (TryExtractGlobalId(relation, out typedId))
                {
                    return true;
                }
            }

            typedId = null;
            return false;
        }

        private static bool TryGetTypedIdViaFallback(IIfcObjectDefinition element, Type runtimeType, out string typedId)
        {
            IfcAccessorTelemetry.TrackFallback(AccessorKind.TypedId, runtimeType);

            var delegates = IfcAccessorDelegateCache.GetOrCreate(runtimeType);
            var typedByValue = delegates.GetTypedBy is null ? null : delegates.GetTypedBy(element);
            return TryExtractGlobalId(typedByValue, out typedId);
        }

        internal static string GetMaterialId(IIfcObjectDefinition objectDefinition)
        {
            var material = objectDefinition.Material;
            if (material is null)
            {
                return null;
            }

            if (TryGetEntityLabel(material, out var directLabel)
                && directLabel.HasValue)
            {
                IfcAccessorTelemetry.TrackFast(AccessorKind.MaterialId);
                var directTypeName = material.ExpressType.Name;
                return $"{directTypeName}_{directLabel.Value}";
            }

            IfcAccessorTelemetry.TrackFallback(AccessorKind.MaterialId, objectDefinition.GetType());

            var delegates = IfcAccessorDelegateCache.GetOrCreate(objectDefinition.GetType());
            var materialValue = delegates.GetMaterial is null ? null : delegates.GetMaterial(objectDefinition);
            if (materialValue is null
                || !TryGetEntityLabel(materialValue, out var fallbackLabel)
                || !fallbackLabel.HasValue)
            {
                return null;
            }

            var fallbackTypeName = materialValue.GetType().Name;
            return $"{fallbackTypeName}_{fallbackLabel.Value}";
        }

        internal static bool TryGetEntityLabel(object value, out int? entityLabel)
        {
            if (value is IPersistEntity persistEntity)
            {
                IfcAccessorTelemetry.TrackFast(AccessorKind.EntityLabel);
                entityLabel = persistEntity.EntityLabel;
                return true;
            }

            IfcAccessorTelemetry.TrackFallback(AccessorKind.EntityLabel, value.GetType());

            var delegates = IfcAccessorDelegateCache.GetOrCreate(value.GetType());
            if (delegates.GetEntityLabel is not null)
            {
                var rawLabel = delegates.GetEntityLabel(value);
                if (rawLabel is int intLabel)
                {
                    entityLabel = intLabel;
                    return true;
                }

            }

            entityLabel = null;
            return false;
        }

        internal static bool TryExtractGlobalId(object value, out string globalId)
        {
            switch (value)
            {
                case null:
                    globalId = null;
                    return false;
                case string stringValue when !string.IsNullOrWhiteSpace(stringValue):
                    IfcAccessorTelemetry.TrackFast(AccessorKind.GlobalId);
                    globalId = stringValue;
                    return true;
                case Xbim.Ifc2x3.UtilityResource.IfcGloballyUniqueId global2x3Id:
                    IfcAccessorTelemetry.TrackFast(AccessorKind.GlobalId);
                    globalId = global2x3Id.Value.ToString();
                    return true;
                case Xbim.Ifc4.UtilityResource.IfcGloballyUniqueId global4Id:
                    IfcAccessorTelemetry.TrackFast(AccessorKind.GlobalId);
                    globalId = global4Id.Value.ToString();
                    return true;
                case IIfcRelDefinesByType relation:
                    return TryExtractGlobalId(relation.RelatingType, out globalId);
                case IIfcRoot ifcRoot when !string.IsNullOrWhiteSpace(ifcRoot.GlobalId):
                    IfcAccessorTelemetry.TrackFast(AccessorKind.GlobalId);
                    globalId = ifcRoot.GlobalId;
                    return true;
                case IEnumerable collection:
                    foreach (var item in collection)
                    {
                        if (TryExtractGlobalId(item, out globalId))
                        {
                            return true;
                        }
                    }

                    globalId = null;
                    return false;
                default:
                    IfcAccessorTelemetry.TrackFallback(AccessorKind.GlobalId, value.GetType());

                    var delegates = IfcAccessorDelegateCache.GetOrCreate(value.GetType());
                    if (delegates.GetGlobalId is null)
                    {
                        globalId = null;
                        return false;
                    }

                    var rawGlobalId = delegates.GetGlobalId(value);
                    if (ReferenceEquals(rawGlobalId, value))
                    {
                        globalId = null;
                        return false;
                    }

                    return TryExtractGlobalId(rawGlobalId, out globalId);
            }
        }
    }

    internal enum TypedIdStrategy
    {
        AlwaysNull,
        DirectTypedByHotIfc2x3,
        FallbackDelegate,
    }

    internal enum AccessorKind
    {
        TypedId,
        MaterialId,
        EntityLabel,
        GlobalId,
    }

    internal static class IfcAccessorTelemetry
    {
        private static long _typedIdFastHits;
        private static long _typedIdFallbackHits;
        private static long _materialIdFastHits;
        private static long _materialIdFallbackHits;
        private static long _entityLabelFastHits;
        private static long _entityLabelFallbackHits;
        private static long _globalIdFastHits;
        private static long _globalIdFallbackHits;

        private static readonly ConcurrentDictionary<string, long> FallbackTypeHits = new(StringComparer.Ordinal);

        internal static void TrackFast(AccessorKind kind)
        {
            switch (kind)
            {
                case AccessorKind.TypedId:
                    Interlocked.Increment(ref _typedIdFastHits);
                    break;
                case AccessorKind.MaterialId:
                    Interlocked.Increment(ref _materialIdFastHits);
                    break;
                case AccessorKind.EntityLabel:
                    Interlocked.Increment(ref _entityLabelFastHits);
                    break;
                case AccessorKind.GlobalId:
                    Interlocked.Increment(ref _globalIdFastHits);
                    break;
            }
        }

        internal static void TrackFallback(AccessorKind kind, Type type)
        {
            switch (kind)
            {
                case AccessorKind.TypedId:
                    Interlocked.Increment(ref _typedIdFallbackHits);
                    break;
                case AccessorKind.MaterialId:
                    Interlocked.Increment(ref _materialIdFallbackHits);
                    break;
                case AccessorKind.EntityLabel:
                    Interlocked.Increment(ref _entityLabelFallbackHits);
                    break;
                case AccessorKind.GlobalId:
                    Interlocked.Increment(ref _globalIdFallbackHits);
                    break;
            }

            var key = type.FullName ?? type.Name;
            FallbackTypeHits.AddOrUpdate(key, 1, static (_, current) => current + 1);
        }

        internal static IfcAccessorTelemetrySnapshot GetSnapshot()
        {
            return new IfcAccessorTelemetrySnapshot(
                Interlocked.Read(ref _typedIdFastHits),
                Interlocked.Read(ref _typedIdFallbackHits),
                Interlocked.Read(ref _materialIdFastHits),
                Interlocked.Read(ref _materialIdFallbackHits),
                Interlocked.Read(ref _entityLabelFastHits),
                Interlocked.Read(ref _entityLabelFallbackHits),
                Interlocked.Read(ref _globalIdFastHits),
                Interlocked.Read(ref _globalIdFallbackHits),
                new Dictionary<string, long>(FallbackTypeHits));
        }

        internal static void Reset()
        {
            Interlocked.Exchange(ref _typedIdFastHits, 0);
            Interlocked.Exchange(ref _typedIdFallbackHits, 0);
            Interlocked.Exchange(ref _materialIdFastHits, 0);
            Interlocked.Exchange(ref _materialIdFallbackHits, 0);
            Interlocked.Exchange(ref _entityLabelFastHits, 0);
            Interlocked.Exchange(ref _entityLabelFallbackHits, 0);
            Interlocked.Exchange(ref _globalIdFastHits, 0);
            Interlocked.Exchange(ref _globalIdFallbackHits, 0);
            FallbackTypeHits.Clear();
        }
    }

    internal readonly struct IfcAccessorTelemetrySnapshot
    {
        internal IfcAccessorTelemetrySnapshot(
            long typedIdFastHits,
            long typedIdFallbackHits,
            long materialIdFastHits,
            long materialIdFallbackHits,
            long entityLabelFastHits,
            long entityLabelFallbackHits,
            long globalIdFastHits,
            long globalIdFallbackHits,
            IReadOnlyDictionary<string, long> fallbackTypeHits)
        {
            TypedIdFastHits = typedIdFastHits;
            TypedIdFallbackHits = typedIdFallbackHits;
            MaterialIdFastHits = materialIdFastHits;
            MaterialIdFallbackHits = materialIdFallbackHits;
            EntityLabelFastHits = entityLabelFastHits;
            EntityLabelFallbackHits = entityLabelFallbackHits;
            GlobalIdFastHits = globalIdFastHits;
            GlobalIdFallbackHits = globalIdFallbackHits;
            FallbackTypeHits = fallbackTypeHits;
        }

        internal long TypedIdFastHits { get; }

        internal long TypedIdFallbackHits { get; }

        internal long MaterialIdFastHits { get; }

        internal long MaterialIdFallbackHits { get; }

        internal long EntityLabelFastHits { get; }

        internal long EntityLabelFallbackHits { get; }

        internal long GlobalIdFastHits { get; }

        internal long GlobalIdFallbackHits { get; }

        internal IReadOnlyDictionary<string, long> FallbackTypeHits { get; }
    }

    internal static class IfcAccessorDelegateCache
    {
        private static readonly ConcurrentDictionary<Type, AccessorDelegates> Cache = new();

        internal static AccessorDelegates GetOrCreate(Type type)
        {
            return Cache.GetOrAdd(type, static t => new AccessorDelegates(
                CreateGetter(t, "IsTypedBy"),
                CreateGetter(t, "Material"),
                CreateGetter(t, "GlobalId"),
                CreateGetter(t, "EntityLabel")));
        }

        private static Func<object, object> CreateGetter(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || property.GetIndexParameters().Length > 0)
            {
                return null;
            }

            var instance = Expression.Parameter(typeof(object), "instance");
            var typedInstance = Expression.Convert(instance, type);
            var propertyAccess = Expression.Property(typedInstance, property);
            var boxedProperty = Expression.Convert(propertyAccess, typeof(object));
            return Expression.Lambda<Func<object, object>>(boxedProperty, instance).Compile();
        }

        internal readonly struct AccessorDelegates
        {
            internal AccessorDelegates(
                Func<object, object> getTypedBy,
                Func<object, object> getMaterial,
                Func<object, object> getGlobalId,
                Func<object, object> getEntityLabel)
            {
                GetTypedBy = getTypedBy;
                GetMaterial = getMaterial;
                GetGlobalId = getGlobalId;
                GetEntityLabel = getEntityLabel;
            }

            internal Func<object, object> GetTypedBy { get; }

            internal Func<object, object> GetMaterial { get; }

            internal Func<object, object> GetGlobalId { get; }

            internal Func<object, object> GetEntityLabel { get; }
        }
    }
}
