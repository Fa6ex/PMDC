﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PMDC.Dungeon;
using RogueEssence.Dev;
using RogueEssence.LevelGen;

namespace PMDC.Dev
{
    public class SerializerContractResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            FieldInfo[] fieldsLess = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            List<MemberInfo> fields = type.GetSerializableMembers();
            List<JsonProperty> props = fields.Select(f => CreateProperty(f, memberSerialization))
                .ToList();
            props.ForEach(p => { p.Writable = true; p.Readable = true; });
            return props;

        }

        //NOTE this didn't work as expected either... find out why later.
        //protected override JsonConverter ResolveContractConverter(Type objectType)
        //{
        //    if (objectType.Equals(typeof(ElementMobilityEvent)))
        //        return new ElementMobilityEventConverter();
        //    return base.ResolveContractConverter(objectType);
        //}
    }
}
