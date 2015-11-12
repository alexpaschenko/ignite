/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

// ReSharper disable UnassignedField.Global
// ReSharper disable CollectionNeverUpdated.Global
namespace Apache.Ignite.Core.Tests.Portable
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Apache.Ignite.Core.Binary;
    using Apache.Ignite.Core.Impl;
    using Apache.Ignite.Core.Impl.Binary;
    using NUnit.Framework;

    /// <summary>
    /// Portable builder self test.
    /// </summary>
    public class PortableApiSelfTest
    {
        /** Undefined type: Empty. */
        private const string TypeEmpty = "EmptyUndefined";

        /** Grid. */
        private Ignite _grid;

        /** Marshaller. */
        private Marshaller _marsh;

        /// <summary>
        /// Set up routine.
        /// </summary>
        [TestFixtureSetUp]
        public void SetUp()
        {
            TestUtils.KillProcesses();

            var cfg = new IgniteConfiguration
            {
                BinaryConfiguration = new BinaryConfiguration
                {
                    TypeConfigurations = new List<BinaryTypeConfiguration>
                    {
                        new BinaryTypeConfiguration(typeof (Empty)),
                        new BinaryTypeConfiguration(typeof (Primitives)),
                        new BinaryTypeConfiguration(typeof (PrimitiveArrays)),
                        new BinaryTypeConfiguration(typeof (StringDateGuidEnum)),
                        new BinaryTypeConfiguration(typeof (WithRaw)),
                        new BinaryTypeConfiguration(typeof (MetaOverwrite)),
                        new BinaryTypeConfiguration(typeof (NestedOuter)),
                        new BinaryTypeConfiguration(typeof (NestedInner)),
                        new BinaryTypeConfiguration(typeof (MigrationOuter)),
                        new BinaryTypeConfiguration(typeof (MigrationInner)),
                        new BinaryTypeConfiguration(typeof (InversionOuter)),
                        new BinaryTypeConfiguration(typeof (InversionInner)),
                        new BinaryTypeConfiguration(typeof (CompositeOuter)),
                        new BinaryTypeConfiguration(typeof (CompositeInner)),
                        new BinaryTypeConfiguration(typeof (CompositeArray)),
                        new BinaryTypeConfiguration(typeof (CompositeContainer)),
                        new BinaryTypeConfiguration(typeof (ToPortable)),
                        new BinaryTypeConfiguration(typeof (Remove)),
                        new BinaryTypeConfiguration(typeof (RemoveInner)),
                        new BinaryTypeConfiguration(typeof (BuilderInBuilderOuter)),
                        new BinaryTypeConfiguration(typeof (BuilderInBuilderInner)),
                        new BinaryTypeConfiguration(typeof (BuilderCollection)),
                        new BinaryTypeConfiguration(typeof (BuilderCollectionItem)),
                        new BinaryTypeConfiguration(typeof (DecimalHolder)),
                        new BinaryTypeConfiguration(TypeEmpty),
                        TypeConfigurationNoMeta(typeof (EmptyNoMeta)),
                        TypeConfigurationNoMeta(typeof (ToPortableNoMeta))
                    },
                    DefaultIdMapper = new IdMapper()
                },
                JvmClasspath = TestUtils.CreateTestClasspath(),
                JvmOptions = new List<string>
                {
                    "-ea",
                    "-Xcheck:jni",
                    "-Xms4g",
                    "-Xmx4g",
                    "-DIGNITE_QUIET=false",
                    "-Xnoagent",
                    "-Djava.compiler=NONE",
                    "-Xdebug",
                    "-Xrunjdwp:transport=dt_socket,server=y,suspend=n,address=5005",
                    "-XX:+HeapDumpOnOutOfMemoryError"
                },
                SpringConfigUrl = "config\\portable.xml"
            };

            _grid = (Ignite) Ignition.Start(cfg);

            _marsh = _grid.Marshaller;
        }

        /// <summary>
        /// Tear down routine.
        /// </summary>
        [TestFixtureTearDown]
        public virtual void TearDown()
        {
            if (_grid != null)
                Ignition.Stop(_grid.Name, true);

            _grid = null;
        }

        /// <summary>
        /// Ensure that portable engine is able to work with type names, which are not configured.
        /// </summary>
        [Test]
        public void TestNonConfigured()
        {
            string typeName1 = "Type1";
            string typeName2 = "Type2";
            string field1 = "field1";
            string field2 = "field2";

            // 1. Ensure that builder works fine.
            IBinaryObject portObj1 = _grid.GetBinary().GetBuilder(typeName1).SetField(field1, 1).Build();

            Assert.AreEqual(typeName1, portObj1.GetBinaryType().TypeName);
            Assert.AreEqual(1, portObj1.GetBinaryType().Fields.Count);
            Assert.AreEqual(field1, portObj1.GetBinaryType().Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameInt, portObj1.GetBinaryType().GetFieldTypeName(field1));

            Assert.AreEqual(1, portObj1.GetField<int>(field1));

            // 2. Ensure that object can be unmarshalled without deserialization.
            byte[] data = ((BinaryObject) portObj1).Data;

            portObj1 = _grid.Marshaller.Unmarshal<IBinaryObject>(data, BinaryMode.ForceBinary);

            Assert.AreEqual(typeName1, portObj1.GetBinaryType().TypeName);
            Assert.AreEqual(1, portObj1.GetBinaryType().Fields.Count);
            Assert.AreEqual(field1, portObj1.GetBinaryType().Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameInt, portObj1.GetBinaryType().GetFieldTypeName(field1));

            Assert.AreEqual(1, portObj1.GetField<int>(field1));

            // 3. Ensure that we can nest one anonymous object inside another
            IBinaryObject portObj2 =
                _grid.GetBinary().GetBuilder(typeName2).SetField(field2, portObj1).Build();

            Assert.AreEqual(typeName2, portObj2.GetBinaryType().TypeName);
            Assert.AreEqual(1, portObj2.GetBinaryType().Fields.Count);
            Assert.AreEqual(field2, portObj2.GetBinaryType().Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameObject, portObj2.GetBinaryType().GetFieldTypeName(field2));

            portObj1 = portObj2.GetField<IBinaryObject>(field2);

            Assert.AreEqual(typeName1, portObj1.GetBinaryType().TypeName);
            Assert.AreEqual(1, portObj1.GetBinaryType().Fields.Count);
            Assert.AreEqual(field1, portObj1.GetBinaryType().Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameInt, portObj1.GetBinaryType().GetFieldTypeName(field1));

            Assert.AreEqual(1, portObj1.GetField<int>(field1));

            // 4. Ensure that we can unmarshal object with other nested object.
            data = ((BinaryObject) portObj2).Data;

            portObj2 = _grid.Marshaller.Unmarshal<IBinaryObject>(data, BinaryMode.ForceBinary);

            Assert.AreEqual(typeName2, portObj2.GetBinaryType().TypeName);
            Assert.AreEqual(1, portObj2.GetBinaryType().Fields.Count);
            Assert.AreEqual(field2, portObj2.GetBinaryType().Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameObject, portObj2.GetBinaryType().GetFieldTypeName(field2));

            portObj1 = portObj2.GetField<IBinaryObject>(field2);

            Assert.AreEqual(typeName1, portObj1.GetBinaryType().TypeName);
            Assert.AreEqual(1, portObj1.GetBinaryType().Fields.Count);
            Assert.AreEqual(field1, portObj1.GetBinaryType().Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameInt, portObj1.GetBinaryType().GetFieldTypeName(field1));

            Assert.AreEqual(1, portObj1.GetField<int>(field1));
        }

        /// <summary>
        /// Test "ToPortable()" method.
        /// </summary>
        [Test]
        public void TestToPortable()
        {
            DateTime date = DateTime.Now.ToUniversalTime();
            Guid guid = Guid.NewGuid();

            IIgniteBinary api = _grid.GetBinary();

            // 1. Primitives.
            Assert.AreEqual(1, api.ToBinary<byte>((byte)1));
            Assert.AreEqual(1, api.ToBinary<short>((short)1));
            Assert.AreEqual(1, api.ToBinary<int>(1));
            Assert.AreEqual(1, api.ToBinary<long>((long)1));

            Assert.AreEqual((float)1, api.ToBinary<float>((float)1));
            Assert.AreEqual((double)1, api.ToBinary<double>((double)1));

            Assert.AreEqual(true, api.ToBinary<bool>(true));
            Assert.AreEqual('a', api.ToBinary<char>('a'));

            // 2. Special types.
            Assert.AreEqual("a", api.ToBinary<string>("a"));
            Assert.AreEqual(date, api.ToBinary<DateTime>(date));
            Assert.AreEqual(guid, api.ToBinary<Guid>(guid));
            Assert.AreEqual(TestEnum.One, api.ToBinary<TestEnum>(TestEnum.One));

            // 3. Arrays.
            Assert.AreEqual(new byte[] { 1 }, api.ToBinary<byte[]>(new byte[] { 1 }));
            Assert.AreEqual(new short[] { 1 }, api.ToBinary<short[]>(new short[] { 1 }));
            Assert.AreEqual(new[] { 1 }, api.ToBinary<int[]>(new[] { 1 }));
            Assert.AreEqual(new long[] { 1 }, api.ToBinary<long[]>(new long[] { 1 }));

            Assert.AreEqual(new float[] { 1 }, api.ToBinary<float[]>(new float[] { 1 }));
            Assert.AreEqual(new double[] { 1 }, api.ToBinary<double[]>(new double[] { 1 }));

            Assert.AreEqual(new[] { true }, api.ToBinary<bool[]>(new[] { true }));
            Assert.AreEqual(new[] { 'a' }, api.ToBinary<char[]>(new[] { 'a' }));

            Assert.AreEqual(new[] { "a" }, api.ToBinary<string[]>(new[] { "a" }));
            Assert.AreEqual(new[] { date }, api.ToBinary<DateTime[]>(new[] { date }));
            Assert.AreEqual(new[] { guid }, api.ToBinary<Guid[]>(new[] { guid }));
            Assert.AreEqual(new[] { TestEnum.One }, api.ToBinary<TestEnum[]>(new[] { TestEnum.One }));

            // 4. Objects.
            IBinaryObject portObj = api.ToBinary<IBinaryObject>(new ToPortable(1));

            Assert.AreEqual(typeof(ToPortable).Name, portObj.GetBinaryType().TypeName);
            Assert.AreEqual(1, portObj.GetBinaryType().Fields.Count);
            Assert.AreEqual("Val", portObj.GetBinaryType().Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameInt, portObj.GetBinaryType().GetFieldTypeName("Val"));

            Assert.AreEqual(1, portObj.GetField<int>("val"));
            Assert.AreEqual(1, portObj.Deserialize<ToPortable>().Val);

            portObj = api.ToBinary<IBinaryObject>(new ToPortableNoMeta(1));

            Assert.AreEqual(1, portObj.GetBinaryType().Fields.Count);

            Assert.AreEqual(1, portObj.GetField<int>("Val"));
            Assert.AreEqual(1, portObj.Deserialize<ToPortableNoMeta>().Val);

            // 5. Object array.
            var portObjArr = api.ToBinary<object[]>(new object[] {new ToPortable(1)})
                .OfType<IBinaryObject>().ToArray();

            Assert.AreEqual(1, portObjArr.Length);
            Assert.AreEqual(1, portObjArr[0].GetField<int>("Val"));
            Assert.AreEqual(1, portObjArr[0].Deserialize<ToPortable>().Val);
        }

        /// <summary>
        /// Test builder field remove logic.
        /// </summary>
        [Test]
        public void TestRemove()
        {
            // Create empty object.
            IBinaryObject portObj = _grid.GetBinary().GetBuilder(typeof(Remove)).Build();

            Assert.IsNull(portObj.GetField<object>("val"));
            Assert.IsNull(portObj.Deserialize<Remove>().Val);

            IBinaryType meta = portObj.GetBinaryType();

            Assert.AreEqual(typeof(Remove).Name, meta.TypeName);
            Assert.AreEqual(0, meta.Fields.Count);

            // Populate it with field.
            IBinaryObjectBuilder builder = _grid.GetBinary().GetBuilder(portObj);

            Assert.IsNull(builder.GetField<object>("val"));

            object val = 1;

            builder.SetField("val", val);

            Assert.AreEqual(val, builder.GetField<object>("val"));

            portObj = builder.Build();

            Assert.AreEqual(val, portObj.GetField<object>("val"));
            Assert.AreEqual(val, portObj.Deserialize<Remove>().Val);

            meta = portObj.GetBinaryType();

            Assert.AreEqual(typeof(Remove).Name, meta.TypeName);
            Assert.AreEqual(1, meta.Fields.Count);
            Assert.AreEqual("val", meta.Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameObject, meta.GetFieldTypeName("val"));

            // Perform field remove.
            builder = _grid.GetBinary().GetBuilder(portObj);

            Assert.AreEqual(val, builder.GetField<object>("val"));

            builder.RemoveField("val");
            Assert.IsNull(builder.GetField<object>("val"));

            builder.SetField("val", val);
            Assert.AreEqual(val, builder.GetField<object>("val"));

            builder.RemoveField("val");
            Assert.IsNull(builder.GetField<object>("val"));

            portObj = builder.Build();

            Assert.IsNull(portObj.GetField<object>("val"));
            Assert.IsNull(portObj.Deserialize<Remove>().Val);

            // Test correct removal of field being referenced by handle somewhere else.
            RemoveInner inner = new RemoveInner(2);

            portObj = _grid.GetBinary().GetBuilder(typeof(Remove))
                .SetField("val", inner)
                .SetField("val2", inner)
                .Build();

            portObj = _grid.GetBinary().GetBuilder(portObj).RemoveField("val").Build();

            Remove obj = portObj.Deserialize<Remove>();

            Assert.IsNull(obj.Val);
            Assert.AreEqual(2, obj.Val2.Val);
        }

        /// <summary>
        /// Test builder-in-builder scenario.
        /// </summary>
        [Test]
        public void TestBuilderInBuilder()
        {
            // Test different builders assembly.
            IBinaryObjectBuilder builderOuter = _grid.GetBinary().GetBuilder(typeof(BuilderInBuilderOuter));
            IBinaryObjectBuilder builderInner = _grid.GetBinary().GetBuilder(typeof(BuilderInBuilderInner));

            builderOuter.SetField<object>("inner", builderInner);
            builderInner.SetField<object>("outer", builderOuter);

            IBinaryObject outerPortObj = builderOuter.Build();

            IBinaryType meta = outerPortObj.GetBinaryType();

            Assert.AreEqual(typeof(BuilderInBuilderOuter).Name, meta.TypeName);
            Assert.AreEqual(1, meta.Fields.Count);
            Assert.AreEqual("inner", meta.Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameObject, meta.GetFieldTypeName("inner"));

            IBinaryObject innerPortObj = outerPortObj.GetField<IBinaryObject>("inner");

            meta = innerPortObj.GetBinaryType();

            Assert.AreEqual(typeof(BuilderInBuilderInner).Name, meta.TypeName);
            Assert.AreEqual(1, meta.Fields.Count);
            Assert.AreEqual("outer", meta.Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameObject, meta.GetFieldTypeName("outer"));

            BuilderInBuilderOuter outer = outerPortObj.Deserialize<BuilderInBuilderOuter>();

            Assert.AreSame(outer, outer.Inner.Outer);

            // Test same builders assembly.
            innerPortObj = _grid.GetBinary().GetBuilder(typeof(BuilderInBuilderInner)).Build();

            outerPortObj = _grid.GetBinary().GetBuilder(typeof(BuilderInBuilderOuter))
                .SetField("inner", innerPortObj)
                .SetField("inner2", innerPortObj)
                .Build();

            meta = outerPortObj.GetBinaryType();

            Assert.AreEqual(typeof(BuilderInBuilderOuter).Name, meta.TypeName);
            Assert.AreEqual(2, meta.Fields.Count);
            Assert.IsTrue(meta.Fields.Contains("inner"));
            Assert.IsTrue(meta.Fields.Contains("inner2"));
            Assert.AreEqual(BinaryTypeNames.TypeNameObject, meta.GetFieldTypeName("inner"));
            Assert.AreEqual(BinaryTypeNames.TypeNameObject, meta.GetFieldTypeName("inner2"));

            outer = outerPortObj.Deserialize<BuilderInBuilderOuter>();

            Assert.AreSame(outer.Inner, outer.Inner2);

            builderOuter = _grid.GetBinary().GetBuilder(outerPortObj);
            IBinaryObjectBuilder builderInner2 = builderOuter.GetField<IBinaryObjectBuilder>("inner2");

            builderInner2.SetField("outer", builderOuter);

            outerPortObj = builderOuter.Build();

            outer = outerPortObj.Deserialize<BuilderInBuilderOuter>();

            Assert.AreSame(outer, outer.Inner.Outer);
            Assert.AreSame(outer.Inner, outer.Inner2);
        }

        /// <summary>
        /// Test for decimals building.
        /// </summary>
        [Test]
        public void TestDecimals()
        {
            IBinaryObject portObj = _grid.GetBinary().GetBuilder(typeof(DecimalHolder))
                .SetField("val", decimal.One)
                .SetField("valArr", new decimal?[] { decimal.MinusOne })
                .Build();

            IBinaryType meta = portObj.GetBinaryType();

            Assert.AreEqual(typeof(DecimalHolder).Name, meta.TypeName);
            Assert.AreEqual(2, meta.Fields.Count);
            Assert.IsTrue(meta.Fields.Contains("val"));
            Assert.IsTrue(meta.Fields.Contains("valArr"));
            Assert.AreEqual(BinaryTypeNames.TypeNameDecimal, meta.GetFieldTypeName("val"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayDecimal, meta.GetFieldTypeName("valArr"));

            Assert.AreEqual(decimal.One, portObj.GetField<decimal>("val"));
            Assert.AreEqual(new decimal?[] { decimal.MinusOne }, portObj.GetField<decimal?[]>("valArr"));

            DecimalHolder obj = portObj.Deserialize<DecimalHolder>();

            Assert.AreEqual(decimal.One, obj.Val);
            Assert.AreEqual(new decimal?[] { decimal.MinusOne }, obj.ValArr);
        }

        /// <summary>
        /// Test for an object returning collection of builders.
        /// </summary>
        [Test]
        public void TestBuilderCollection()
        {
            // Test collection with single element.
            IBinaryObjectBuilder builderCol = _grid.GetBinary().GetBuilder(typeof(BuilderCollection));
            IBinaryObjectBuilder builderItem =
                _grid.GetBinary().GetBuilder(typeof(BuilderCollectionItem)).SetField("val", 1);

            builderCol.SetCollectionField("col", new ArrayList { builderItem });

            IBinaryObject portCol = builderCol.Build();

            IBinaryType meta = portCol.GetBinaryType();

            Assert.AreEqual(typeof(BuilderCollection).Name, meta.TypeName);
            Assert.AreEqual(1, meta.Fields.Count);
            Assert.AreEqual("col", meta.Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameCollection, meta.GetFieldTypeName("col"));

            var portColItems = portCol.GetField<ArrayList>("col");

            Assert.AreEqual(1, portColItems.Count);

            var portItem = (IBinaryObject) portColItems[0];

            meta = portItem.GetBinaryType();

            Assert.AreEqual(typeof(BuilderCollectionItem).Name, meta.TypeName);
            Assert.AreEqual(1, meta.Fields.Count);
            Assert.AreEqual("val", meta.Fields.First());
            Assert.AreEqual(BinaryTypeNames.TypeNameInt, meta.GetFieldTypeName("val"));

            BuilderCollection col = portCol.Deserialize<BuilderCollection>();

            Assert.IsNotNull(col.Col);
            Assert.AreEqual(1, col.Col.Count);
            Assert.AreEqual(1, ((BuilderCollectionItem) col.Col[0]).Val);

            // Add more portable objects to collection.
            builderCol = _grid.GetBinary().GetBuilder(portCol);

            IList builderColItems = builderCol.GetField<IList>("col");

            Assert.AreEqual(1, builderColItems.Count);

            BinaryObjectBuilder builderColItem = (BinaryObjectBuilder) builderColItems[0];

            builderColItem.SetField("val", 2); // Change nested value.

            builderColItems.Add(builderColItem); // Add the same object to check handles.
            builderColItems.Add(builderItem); // Add item from another builder.
            builderColItems.Add(portItem); // Add item in portable form.

            portCol = builderCol.Build();

            col = portCol.Deserialize<BuilderCollection>();

            Assert.AreEqual(4, col.Col.Count);

            var item0 = (BuilderCollectionItem) col.Col[0];
            var item1 = (BuilderCollectionItem) col.Col[1];
            var item2 = (BuilderCollectionItem) col.Col[2];
            var item3 = (BuilderCollectionItem) col.Col[3];

            Assert.AreEqual(2, item0.Val);

            Assert.AreSame(item0, item1);
            Assert.AreNotSame(item0, item2);
            Assert.AreNotSame(item0, item3);

            Assert.AreEqual(1, item2.Val);
            Assert.AreEqual(1, item3.Val);

            Assert.AreNotSame(item2, item3);

            // Test handle update inside collection.
            builderCol = _grid.GetBinary().GetBuilder(portCol);

            builderColItems = builderCol.GetField<IList>("col");

            ((BinaryObjectBuilder) builderColItems[1]).SetField("val", 3);

            portCol = builderCol.Build();

            col = portCol.Deserialize<BuilderCollection>();

            item0 = (BuilderCollectionItem) col.Col[0];
            item1 = (BuilderCollectionItem) col.Col[1];

            Assert.AreEqual(3, item0.Val);
            Assert.AreSame(item0, item1);
        }

        /// <summary>
        /// Test build of an empty object.
        /// </summary>
        [Test]
        public void TestEmptyDefined()
        {
            IBinaryObject portObj = _grid.GetBinary().GetBuilder(typeof(Empty)).Build();

            Assert.IsNotNull(portObj);
            Assert.AreEqual(0, portObj.GetHashCode());

            IBinaryType meta = portObj.GetBinaryType();

            Assert.IsNotNull(meta);
            Assert.AreEqual(typeof(Empty).Name, meta.TypeName);
            Assert.AreEqual(0, meta.Fields.Count);

            Empty obj = portObj.Deserialize<Empty>();

            Assert.IsNotNull(obj);
        }

        /// <summary>
        /// Test build of an empty object with disabled metadata.
        /// </summary>
        [Test]
        public void TestEmptyNoMeta()
        {
            IBinaryObject portObj = _grid.GetBinary().GetBuilder(typeof(EmptyNoMeta)).Build();

            Assert.IsNotNull(portObj);
            Assert.AreEqual(0, portObj.GetHashCode());

            EmptyNoMeta obj = portObj.Deserialize<EmptyNoMeta>();

            Assert.IsNotNull(obj);
        }

        /// <summary>
        /// Test build of an empty undefined object.
        /// </summary>
        [Test]
        public void TestEmptyUndefined()
        {
            IBinaryObject portObj = _grid.GetBinary().GetBuilder(TypeEmpty).Build();

            Assert.IsNotNull(portObj);
            Assert.AreEqual(0, portObj.GetHashCode());

            IBinaryType meta = portObj.GetBinaryType();

            Assert.IsNotNull(meta);
            Assert.AreEqual(TypeEmpty, meta.TypeName);
            Assert.AreEqual(0, meta.Fields.Count);
        }

        /// <summary>
        /// Test object rebuild with no changes.
        /// </summary>
        [Test]
        public void TestEmptyRebuild()
        {
            var portObj = (BinaryObject) _grid.GetBinary().GetBuilder(typeof(EmptyNoMeta)).Build();

            BinaryObject newPortObj = (BinaryObject) _grid.GetBinary().GetBuilder(portObj).Build();

            Assert.AreEqual(portObj.Data, newPortObj.Data);
        }

        /// <summary>
        /// Test hash code alteration.
        /// </summary>
        [Test]
        public void TestHashCodeChange()
        {
            IBinaryObject portObj = _grid.GetBinary().GetBuilder(typeof(EmptyNoMeta)).SetHashCode(100).Build();

            Assert.AreEqual(100, portObj.GetHashCode());
        }

        /// <summary>
        /// Test primitive fields setting.
        /// </summary>
        [Test]
        public void TestPrimitiveFields()
        {
            IBinaryObject portObj = _grid.GetBinary().GetBuilder(typeof(Primitives))
                .SetField<byte>("fByte", 1)
                .SetField("fBool", true)
                .SetField<short>("fShort", 2)
                .SetField("fChar", 'a')
                .SetField("fInt", 3)
                .SetField<long>("fLong", 4)
                .SetField<float>("fFloat", 5)
                .SetField<double>("fDouble", 6)
                .SetHashCode(100)
                .Build();

            Assert.AreEqual(100, portObj.GetHashCode());

            IBinaryType meta = portObj.GetBinaryType();

            Assert.AreEqual(typeof(Primitives).Name, meta.TypeName);

            Assert.AreEqual(8, meta.Fields.Count);

            Assert.AreEqual(BinaryTypeNames.TypeNameByte, meta.GetFieldTypeName("fByte"));
            Assert.AreEqual(BinaryTypeNames.TypeNameBool, meta.GetFieldTypeName("fBool"));
            Assert.AreEqual(BinaryTypeNames.TypeNameShort, meta.GetFieldTypeName("fShort"));
            Assert.AreEqual(BinaryTypeNames.TypeNameChar, meta.GetFieldTypeName("fChar"));
            Assert.AreEqual(BinaryTypeNames.TypeNameInt, meta.GetFieldTypeName("fInt"));
            Assert.AreEqual(BinaryTypeNames.TypeNameLong, meta.GetFieldTypeName("fLong"));
            Assert.AreEqual(BinaryTypeNames.TypeNameFloat, meta.GetFieldTypeName("fFloat"));
            Assert.AreEqual(BinaryTypeNames.TypeNameDouble, meta.GetFieldTypeName("fDouble"));

            Assert.AreEqual(1, portObj.GetField<byte>("fByte"));
            Assert.AreEqual(true, portObj.GetField<bool>("fBool"));
            Assert.AreEqual(2, portObj.GetField<short>("fShort"));
            Assert.AreEqual('a', portObj.GetField<char>("fChar"));
            Assert.AreEqual(3, portObj.GetField<int>("fInt"));
            Assert.AreEqual(4, portObj.GetField<long>("fLong"));
            Assert.AreEqual(5, portObj.GetField<float>("fFloat"));
            Assert.AreEqual(6, portObj.GetField<double>("fDouble"));

            Primitives obj = portObj.Deserialize<Primitives>();

            Assert.AreEqual(1, obj.FByte);
            Assert.AreEqual(true, obj.FBool);
            Assert.AreEqual(2, obj.FShort);
            Assert.AreEqual('a', obj.FChar);
            Assert.AreEqual(3, obj.FInt);
            Assert.AreEqual(4, obj.FLong);
            Assert.AreEqual(5, obj.FFloat);
            Assert.AreEqual(6, obj.FDouble);

            // Overwrite.
            portObj = _grid.GetBinary().GetBuilder(portObj)
                .SetField<byte>("fByte", 7)
                .SetField("fBool", false)
                .SetField<short>("fShort", 8)
                .SetField("fChar", 'b')
                .SetField("fInt", 9)
                .SetField<long>("fLong", 10)
                .SetField<float>("fFloat", 11)
                .SetField<double>("fDouble", 12)
                .SetHashCode(200)
                .Build();

            Assert.AreEqual(200, portObj.GetHashCode());

            Assert.AreEqual(7, portObj.GetField<byte>("fByte"));
            Assert.AreEqual(false, portObj.GetField<bool>("fBool"));
            Assert.AreEqual(8, portObj.GetField<short>("fShort"));
            Assert.AreEqual('b', portObj.GetField<char>("fChar"));
            Assert.AreEqual(9, portObj.GetField<int>("fInt"));
            Assert.AreEqual(10, portObj.GetField<long>("fLong"));
            Assert.AreEqual(11, portObj.GetField<float>("fFloat"));
            Assert.AreEqual(12, portObj.GetField<double>("fDouble"));

            obj = portObj.Deserialize<Primitives>();

            Assert.AreEqual(7, obj.FByte);
            Assert.AreEqual(false, obj.FBool);
            Assert.AreEqual(8, obj.FShort);
            Assert.AreEqual('b', obj.FChar);
            Assert.AreEqual(9, obj.FInt);
            Assert.AreEqual(10, obj.FLong);
            Assert.AreEqual(11, obj.FFloat);
            Assert.AreEqual(12, obj.FDouble);
        }

        /// <summary>
        /// Test primitive array fields setting.
        /// </summary>
        [Test]
        public void TestPrimitiveArrayFields()
        {
            IBinaryObject portObj = _grid.GetBinary().GetBuilder(typeof(PrimitiveArrays))
                .SetField("fByte", new byte[] { 1 })
                .SetField("fBool", new[] { true })
                .SetField("fShort", new short[] { 2 })
                .SetField("fChar", new[] { 'a' })
                .SetField("fInt", new[] { 3 })
                .SetField("fLong", new long[] { 4 })
                .SetField("fFloat", new float[] { 5 })
                .SetField("fDouble", new double[] { 6 })
                .SetHashCode(100)
                .Build();

            Assert.AreEqual(100, portObj.GetHashCode());

            IBinaryType meta = portObj.GetBinaryType();

            Assert.AreEqual(typeof(PrimitiveArrays).Name, meta.TypeName);

            Assert.AreEqual(8, meta.Fields.Count);

            Assert.AreEqual(BinaryTypeNames.TypeNameArrayByte, meta.GetFieldTypeName("fByte"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayBool, meta.GetFieldTypeName("fBool"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayShort, meta.GetFieldTypeName("fShort"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayChar, meta.GetFieldTypeName("fChar"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayInt, meta.GetFieldTypeName("fInt"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayLong, meta.GetFieldTypeName("fLong"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayFloat, meta.GetFieldTypeName("fFloat"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayDouble, meta.GetFieldTypeName("fDouble"));

            Assert.AreEqual(new byte[] { 1 }, portObj.GetField<byte[]>("fByte"));
            Assert.AreEqual(new[] { true }, portObj.GetField<bool[]>("fBool"));
            Assert.AreEqual(new short[] { 2 }, portObj.GetField<short[]>("fShort"));
            Assert.AreEqual(new[] { 'a' }, portObj.GetField<char[]>("fChar"));
            Assert.AreEqual(new[] { 3 }, portObj.GetField<int[]>("fInt"));
            Assert.AreEqual(new long[] { 4 }, portObj.GetField<long[]>("fLong"));
            Assert.AreEqual(new float[] { 5 }, portObj.GetField<float[]>("fFloat"));
            Assert.AreEqual(new double[] { 6 }, portObj.GetField<double[]>("fDouble"));

            PrimitiveArrays obj = portObj.Deserialize<PrimitiveArrays>();

            Assert.AreEqual(new byte[] { 1 }, obj.FByte);
            Assert.AreEqual(new[] { true }, obj.FBool);
            Assert.AreEqual(new short[] { 2 }, obj.FShort);
            Assert.AreEqual(new[] { 'a' }, obj.FChar);
            Assert.AreEqual(new[] { 3 }, obj.FInt);
            Assert.AreEqual(new long[] { 4 }, obj.FLong);
            Assert.AreEqual(new float[] { 5 }, obj.FFloat);
            Assert.AreEqual(new double[] { 6 }, obj.FDouble);

            // Overwrite.
            portObj = _grid.GetBinary().GetBuilder(portObj)
                .SetField("fByte", new byte[] { 7 })
                .SetField("fBool", new[] { false })
                .SetField("fShort", new short[] { 8 })
                .SetField("fChar", new[] { 'b' })
                .SetField("fInt", new[] { 9 })
                .SetField("fLong", new long[] { 10 })
                .SetField("fFloat", new float[] { 11 })
                .SetField("fDouble", new double[] { 12 })
                .SetHashCode(200)
                .Build();

            Assert.AreEqual(200, portObj.GetHashCode());

            Assert.AreEqual(new byte[] { 7 }, portObj.GetField<byte[]>("fByte"));
            Assert.AreEqual(new[] { false }, portObj.GetField<bool[]>("fBool"));
            Assert.AreEqual(new short[] { 8 }, portObj.GetField<short[]>("fShort"));
            Assert.AreEqual(new[] { 'b' }, portObj.GetField<char[]>("fChar"));
            Assert.AreEqual(new[] { 9 }, portObj.GetField<int[]>("fInt"));
            Assert.AreEqual(new long[] { 10 }, portObj.GetField<long[]>("fLong"));
            Assert.AreEqual(new float[] { 11 }, portObj.GetField<float[]>("fFloat"));
            Assert.AreEqual(new double[] { 12 }, portObj.GetField<double[]>("fDouble"));

            obj = portObj.Deserialize<PrimitiveArrays>();

            Assert.AreEqual(new byte[] { 7 }, obj.FByte);
            Assert.AreEqual(new[] { false }, obj.FBool);
            Assert.AreEqual(new short[] { 8 }, obj.FShort);
            Assert.AreEqual(new[] { 'b' }, obj.FChar);
            Assert.AreEqual(new[] { 9 }, obj.FInt);
            Assert.AreEqual(new long[] { 10 }, obj.FLong);
            Assert.AreEqual(new float[] { 11 }, obj.FFloat);
            Assert.AreEqual(new double[] { 12 }, obj.FDouble);
        }

        /// <summary>
        /// Test non-primitive fields and their array counterparts.
        /// </summary>
        [Test]
        public void TestStringDateGuidEnum()
        {
            DateTime? nDate = DateTime.Now;

            Guid? nGuid = Guid.NewGuid();

            IBinaryObject portObj = _grid.GetBinary().GetBuilder(typeof(StringDateGuidEnum))
                .SetField("fStr", "str")
                .SetField("fNDate", nDate)
                .SetGuidField("fNGuid", nGuid)
                .SetField("fEnum", TestEnum.One)
                .SetField("fStrArr", new[] { "str" })
                .SetArrayField("fDateArr", new[] { nDate })
                .SetGuidArrayField("fGuidArr", new[] { nGuid })
                .SetField("fEnumArr", new[] { TestEnum.One })
                .SetHashCode(100)
                .Build();

            Assert.AreEqual(100, portObj.GetHashCode());

            IBinaryType meta = portObj.GetBinaryType();

            Assert.AreEqual(typeof(StringDateGuidEnum).Name, meta.TypeName);

            Assert.AreEqual(8, meta.Fields.Count);

            Assert.AreEqual(BinaryTypeNames.TypeNameString, meta.GetFieldTypeName("fStr"));
            Assert.AreEqual(BinaryTypeNames.TypeNameObject, meta.GetFieldTypeName("fNDate"));
            Assert.AreEqual(BinaryTypeNames.TypeNameGuid, meta.GetFieldTypeName("fNGuid"));
            Assert.AreEqual(BinaryTypeNames.TypeNameEnum, meta.GetFieldTypeName("fEnum"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayString, meta.GetFieldTypeName("fStrArr"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayObject, meta.GetFieldTypeName("fDateArr"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayGuid, meta.GetFieldTypeName("fGuidArr"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayEnum, meta.GetFieldTypeName("fEnumArr"));

            Assert.AreEqual("str", portObj.GetField<string>("fStr"));
            Assert.AreEqual(nDate, portObj.GetField<DateTime?>("fNDate"));
            Assert.AreEqual(nGuid, portObj.GetField<Guid?>("fNGuid"));
            Assert.AreEqual(TestEnum.One, portObj.GetField<TestEnum>("fEnum"));
            Assert.AreEqual(new[] { "str" }, portObj.GetField<string[]>("fStrArr"));
            Assert.AreEqual(new[] { nDate }, portObj.GetField<DateTime?[]>("fDateArr"));
            Assert.AreEqual(new[] { nGuid }, portObj.GetField<Guid?[]>("fGuidArr"));
            Assert.AreEqual(new[] { TestEnum.One }, portObj.GetField<TestEnum[]>("fEnumArr"));

            StringDateGuidEnum obj = portObj.Deserialize<StringDateGuidEnum>();

            Assert.AreEqual("str", obj.FStr);
            Assert.AreEqual(nDate, obj.FnDate);
            Assert.AreEqual(nGuid, obj.FnGuid);
            Assert.AreEqual(TestEnum.One, obj.FEnum);
            Assert.AreEqual(new[] { "str" }, obj.FStrArr);
            Assert.AreEqual(new[] { nDate }, obj.FDateArr);
            Assert.AreEqual(new[] { nGuid }, obj.FGuidArr);
            Assert.AreEqual(new[] { TestEnum.One }, obj.FEnumArr);

            // Check builder field caching.
            var builder = _grid.GetBinary().GetBuilder(portObj);

            Assert.AreEqual("str", builder.GetField<string>("fStr"));
            Assert.AreEqual(nDate, builder.GetField<DateTime?>("fNDate"));
            Assert.AreEqual(nGuid, builder.GetField<Guid?>("fNGuid"));
            Assert.AreEqual(TestEnum.One, builder.GetField<TestEnum>("fEnum"));
            Assert.AreEqual(new[] { "str" }, builder.GetField<string[]>("fStrArr"));
            Assert.AreEqual(new[] { nDate }, builder.GetField<DateTime?[]>("fDateArr"));
            Assert.AreEqual(new[] { nGuid }, builder.GetField<Guid?[]>("fGuidArr"));
            Assert.AreEqual(new[] { TestEnum.One }, builder.GetField<TestEnum[]>("fEnumArr"));

            // Check reassemble.
            portObj = builder.Build();

            Assert.AreEqual("str", portObj.GetField<string>("fStr"));
            Assert.AreEqual(nDate, portObj.GetField<DateTime?>("fNDate"));
            Assert.AreEqual(nGuid, portObj.GetField<Guid?>("fNGuid"));
            Assert.AreEqual(TestEnum.One, portObj.GetField<TestEnum>("fEnum"));
            Assert.AreEqual(new[] { "str" }, portObj.GetField<string[]>("fStrArr"));
            Assert.AreEqual(new[] { nDate }, portObj.GetField<DateTime?[]>("fDateArr"));
            Assert.AreEqual(new[] { nGuid }, portObj.GetField<Guid?[]>("fGuidArr"));
            Assert.AreEqual(new[] { TestEnum.One }, portObj.GetField<TestEnum[]>("fEnumArr"));

            obj = portObj.Deserialize<StringDateGuidEnum>();

            Assert.AreEqual("str", obj.FStr);
            Assert.AreEqual(nDate, obj.FnDate);
            Assert.AreEqual(nGuid, obj.FnGuid);
            Assert.AreEqual(TestEnum.One, obj.FEnum);
            Assert.AreEqual(new[] { "str" }, obj.FStrArr);
            Assert.AreEqual(new[] { nDate }, obj.FDateArr);
            Assert.AreEqual(new[] { nGuid }, obj.FGuidArr);
            Assert.AreEqual(new[] { TestEnum.One }, obj.FEnumArr);

            // Overwrite.
            nDate = DateTime.Now.ToUniversalTime();
            nGuid = Guid.NewGuid();

            portObj = builder
                .SetField("fStr", "str2")
                .SetTimestampField("fNDate", nDate)
                .SetField("fNGuid", nGuid)
                .SetField("fEnum", TestEnum.Two)
                .SetField("fStrArr", new[] { "str2" })
                .SetArrayField("fDateArr", new[] { nDate })
                .SetField("fGuidArr", new[] { nGuid })
                .SetField("fEnumArr", new[] { TestEnum.Two })
                .SetHashCode(200)
                .Build();

            Assert.AreEqual(200, portObj.GetHashCode());

            Assert.AreEqual("str2", portObj.GetField<string>("fStr"));
            Assert.AreEqual(nDate, portObj.GetField<DateTime?>("fNDate"));
            Assert.AreEqual(nGuid, portObj.GetField<Guid?>("fNGuid"));
            Assert.AreEqual(TestEnum.Two, portObj.GetField<TestEnum>("fEnum"));
            Assert.AreEqual(new[] { "str2" }, portObj.GetField<string[]>("fStrArr"));
            Assert.AreEqual(new[] { nDate }, portObj.GetField<DateTime?[]>("fDateArr"));
            Assert.AreEqual(new[] { nGuid }, portObj.GetField<Guid?[]>("fGuidArr"));
            Assert.AreEqual(new[] { TestEnum.Two }, portObj.GetField<TestEnum[]>("fEnumArr"));

            obj = portObj.Deserialize<StringDateGuidEnum>();

            Assert.AreEqual("str2", obj.FStr);
            Assert.AreEqual(nDate, obj.FnDate);
            Assert.AreEqual(nGuid, obj.FnGuid);
            Assert.AreEqual(TestEnum.Two, obj.FEnum);
            Assert.AreEqual(new[] { "str2" }, obj.FStrArr);
            Assert.AreEqual(new[] { nDate }, obj.FDateArr);
            Assert.AreEqual(new[] { nGuid }, obj.FGuidArr);
            Assert.AreEqual(new[] { TestEnum.Two }, obj.FEnumArr);
        }

        /// <summary>
        /// Test arrays.
        /// </summary>
        [Test]
        public void TestCompositeArray()
        {
            // 1. Test simple array.
            object[] inArr = { new CompositeInner(1) };

            IBinaryObject portObj = _grid.GetBinary().GetBuilder(typeof(CompositeArray)).SetHashCode(100)
                .SetField("inArr", inArr).Build();

            IBinaryType meta = portObj.GetBinaryType();

            Assert.AreEqual(typeof(CompositeArray).Name, meta.TypeName);
            Assert.AreEqual(1, meta.Fields.Count);
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayObject, meta.GetFieldTypeName("inArr"));

            Assert.AreEqual(100, portObj.GetHashCode());

            IBinaryObject[] portInArr = portObj.GetField<object[]>("inArr").Cast<IBinaryObject>().ToArray();

            Assert.AreEqual(1, portInArr.Length);
            Assert.AreEqual(1, portInArr[0].GetField<int>("val"));

            CompositeArray arr = portObj.Deserialize<CompositeArray>();

            Assert.IsNull(arr.OutArr);
            Assert.AreEqual(1, arr.InArr.Length);
            Assert.AreEqual(1, ((CompositeInner) arr.InArr[0]).Val);

            // 2. Test addition to array.
            portObj = _grid.GetBinary().GetBuilder(portObj).SetHashCode(200)
                .SetField("inArr", new object[] { portInArr[0], null }).Build();

            Assert.AreEqual(200, portObj.GetHashCode());

            portInArr = portObj.GetField<object[]>("inArr").Cast<IBinaryObject>().ToArray();

            Assert.AreEqual(2, portInArr.Length);
            Assert.AreEqual(1, portInArr[0].GetField<int>("val"));
            Assert.IsNull(portInArr[1]);

            arr = portObj.Deserialize<CompositeArray>();

            Assert.IsNull(arr.OutArr);
            Assert.AreEqual(2, arr.InArr.Length);
            Assert.AreEqual(1, ((CompositeInner) arr.InArr[0]).Val);
            Assert.IsNull(arr.InArr[1]);

            portInArr[1] = _grid.GetBinary().GetBuilder(typeof(CompositeInner)).SetField("val", 2).Build();

            portObj = _grid.GetBinary().GetBuilder(portObj).SetHashCode(300)
                .SetField("inArr", portInArr.OfType<object>().ToArray()).Build();

            Assert.AreEqual(300, portObj.GetHashCode());

            portInArr = portObj.GetField<object[]>("inArr").Cast<IBinaryObject>().ToArray();

            Assert.AreEqual(2, portInArr.Length);
            Assert.AreEqual(1, portInArr[0].GetField<int>("val"));
            Assert.AreEqual(2, portInArr[1].GetField<int>("val"));

            arr = portObj.Deserialize<CompositeArray>();

            Assert.IsNull(arr.OutArr);
            Assert.AreEqual(2, arr.InArr.Length);
            Assert.AreEqual(1, ((CompositeInner)arr.InArr[0]).Val);
            Assert.AreEqual(2, ((CompositeInner)arr.InArr[1]).Val);

            // 3. Test top-level handle inversion.
            CompositeInner inner = new CompositeInner(1);

            inArr = new object[] { inner, inner };

            portObj = _grid.GetBinary().GetBuilder(typeof(CompositeArray)).SetHashCode(100)
                .SetField("inArr", inArr).Build();

            Assert.AreEqual(100, portObj.GetHashCode());

            portInArr = portObj.GetField<object[]>("inArr").Cast<IBinaryObject>().ToArray();

            Assert.AreEqual(2, portInArr.Length);
            Assert.AreEqual(1, portInArr[0].GetField<int>("val"));
            Assert.AreEqual(1, portInArr[1].GetField<int>("val"));

            arr = portObj.Deserialize<CompositeArray>();

            Assert.IsNull(arr.OutArr);
            Assert.AreEqual(2, arr.InArr.Length);
            Assert.AreEqual(1, ((CompositeInner)arr.InArr[0]).Val);
            Assert.AreEqual(1, ((CompositeInner)arr.InArr[1]).Val);

            portInArr[0] = _grid.GetBinary().GetBuilder(typeof(CompositeInner)).SetField("val", 2).Build();

            portObj = _grid.GetBinary().GetBuilder(portObj).SetHashCode(200)
                .SetField("inArr", portInArr.ToArray<object>()).Build();

            Assert.AreEqual(200, portObj.GetHashCode());

            portInArr = portObj.GetField<object[]>("inArr").Cast<IBinaryObject>().ToArray();

            Assert.AreEqual(2, portInArr.Length);
            Assert.AreEqual(2, portInArr[0].GetField<int>("val"));
            Assert.AreEqual(1, portInArr[1].GetField<int>("val"));

            arr = portObj.Deserialize<CompositeArray>();

            Assert.IsNull(arr.OutArr);
            Assert.AreEqual(2, arr.InArr.Length);
            Assert.AreEqual(2, ((CompositeInner)arr.InArr[0]).Val);
            Assert.AreEqual(1, ((CompositeInner)arr.InArr[1]).Val);

            // 4. Test nested object handle inversion.
            CompositeOuter[] outArr = { new CompositeOuter(inner), new CompositeOuter(inner) };

            portObj = _grid.GetBinary().GetBuilder(typeof(CompositeArray)).SetHashCode(100)
                .SetField("outArr", outArr.ToArray<object>()).Build();

            meta = portObj.GetBinaryType();

            Assert.AreEqual(typeof(CompositeArray).Name, meta.TypeName);
            Assert.AreEqual(2, meta.Fields.Count);
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayObject, meta.GetFieldTypeName("inArr"));
            Assert.AreEqual(BinaryTypeNames.TypeNameArrayObject, meta.GetFieldTypeName("outArr"));

            Assert.AreEqual(100, portObj.GetHashCode());

            var portOutArr = portObj.GetField<object[]>("outArr").Cast<IBinaryObject>().ToArray();

            Assert.AreEqual(2, portOutArr.Length);
            Assert.AreEqual(1, portOutArr[0].GetField<IBinaryObject>("inner").GetField<int>("val"));
            Assert.AreEqual(1, portOutArr[1].GetField<IBinaryObject>("inner").GetField<int>("val"));

            arr = portObj.Deserialize<CompositeArray>();

            Assert.IsNull(arr.InArr);
            Assert.AreEqual(2, arr.OutArr.Length);
            Assert.AreEqual(1, ((CompositeOuter) arr.OutArr[0]).Inner.Val);
            Assert.AreEqual(1, ((CompositeOuter) arr.OutArr[0]).Inner.Val);

            portOutArr[0] = _grid.GetBinary().GetBuilder(typeof(CompositeOuter))
                .SetField("inner", new CompositeInner(2)).Build();

            portObj = _grid.GetBinary().GetBuilder(portObj).SetHashCode(200)
                .SetField("outArr", portOutArr.ToArray<object>()).Build();

            Assert.AreEqual(200, portObj.GetHashCode());

            portInArr = portObj.GetField<object[]>("outArr").Cast<IBinaryObject>().ToArray();

            Assert.AreEqual(2, portInArr.Length);
            Assert.AreEqual(2, portOutArr[0].GetField<IBinaryObject>("inner").GetField<int>("val"));
            Assert.AreEqual(1, portOutArr[1].GetField<IBinaryObject>("inner").GetField<int>("val"));

            arr = portObj.Deserialize<CompositeArray>();

            Assert.IsNull(arr.InArr);
            Assert.AreEqual(2, arr.OutArr.Length);
            Assert.AreEqual(2, ((CompositeOuter)arr.OutArr[0]).Inner.Val);
            Assert.AreEqual(1, ((CompositeOuter)arr.OutArr[1]).Inner.Val);
        }

        /// <summary>
        /// Test container types other than array.
        /// </summary>
        [Test]
        public void TestCompositeContainer()
        {
            ArrayList col = new ArrayList();
            IDictionary dict = new Hashtable();

            col.Add(new CompositeInner(1));
            dict[3] = new CompositeInner(3);

            IBinaryObject portObj = _grid.GetBinary().GetBuilder(typeof(CompositeContainer)).SetHashCode(100)
                .SetCollectionField("col", col)
                .SetDictionaryField("dict", dict).Build();

            // 1. Check meta.
            IBinaryType meta = portObj.GetBinaryType();

            Assert.AreEqual(typeof(CompositeContainer).Name, meta.TypeName);

            Assert.AreEqual(2, meta.Fields.Count);
            Assert.AreEqual(BinaryTypeNames.TypeNameCollection, meta.GetFieldTypeName("col"));
            Assert.AreEqual(BinaryTypeNames.TypeNameMap, meta.GetFieldTypeName("dict"));

            // 2. Check in portable form.
            Assert.AreEqual(1, portObj.GetField<ICollection>("col").Count);
            Assert.AreEqual(1, portObj.GetField<ICollection>("col").OfType<IBinaryObject>().First()
                .GetField<int>("val"));

            Assert.AreEqual(1, portObj.GetField<IDictionary>("dict").Count);
            Assert.AreEqual(3, ((IBinaryObject) portObj.GetField<IDictionary>("dict")[3]).GetField<int>("val"));

            // 3. Check in deserialized form.
            CompositeContainer obj = portObj.Deserialize<CompositeContainer>();

            Assert.AreEqual(1, obj.Col.Count);
            Assert.AreEqual(1, obj.Col.OfType<CompositeInner>().First().Val);

            Assert.AreEqual(1, obj.Dict.Count);
            Assert.AreEqual(3, ((CompositeInner) obj.Dict[3]).Val);
        }

        /// <summary>
        /// Ensure that raw data is not lost during build.
        /// </summary>
        [Test]
        public void TestRawData()
        {
            var raw = new WithRaw
            {
                A = 1,
                B = 2
            };

            var portObj = _marsh.Unmarshal<IBinaryObject>(_marsh.Marshal(raw), BinaryMode.ForceBinary);

            raw = portObj.Deserialize<WithRaw>();

            Assert.AreEqual(1, raw.A);
            Assert.AreEqual(2, raw.B);

            IBinaryObject newPortObj = _grid.GetBinary().GetBuilder(portObj).SetField("a", 3).Build();

            raw = newPortObj.Deserialize<WithRaw>();

            Assert.AreEqual(3, raw.A);
            Assert.AreEqual(2, raw.B);
        }

        /// <summary>
        /// Test nested objects.
        /// </summary>
        [Test]
        public void TestNested()
        {
            // 1. Create from scratch.
            IBinaryObjectBuilder builder = _grid.GetBinary().GetBuilder(typeof(NestedOuter));

            NestedInner inner1 = new NestedInner {Val = 1};
            builder.SetField("inner1", inner1);

            IBinaryObject outerPortObj = builder.Build();

            IBinaryType meta = outerPortObj.GetBinaryType();

            Assert.AreEqual(typeof(NestedOuter).Name, meta.TypeName);
            Assert.AreEqual(1, meta.Fields.Count);
            Assert.AreEqual(BinaryTypeNames.TypeNameObject, meta.GetFieldTypeName("inner1"));

            IBinaryObject innerPortObj1 = outerPortObj.GetField<IBinaryObject>("inner1");

            IBinaryType innerMeta = innerPortObj1.GetBinaryType();

            Assert.AreEqual(typeof(NestedInner).Name, innerMeta.TypeName);
            Assert.AreEqual(1, innerMeta.Fields.Count);
            Assert.AreEqual(BinaryTypeNames.TypeNameInt, innerMeta.GetFieldTypeName("Val"));

            inner1 = innerPortObj1.Deserialize<NestedInner>();

            Assert.AreEqual(1, inner1.Val);

            NestedOuter outer = outerPortObj.Deserialize<NestedOuter>();
            Assert.AreEqual(outer.Inner1.Val, 1);
            Assert.IsNull(outer.Inner2);

            // 2. Add another field over existing portable object.
            builder = _grid.GetBinary().GetBuilder(outerPortObj);

            NestedInner inner2 = new NestedInner {Val = 2};
            builder.SetField("inner2", inner2);

            outerPortObj = builder.Build();

            outer = outerPortObj.Deserialize<NestedOuter>();
            Assert.AreEqual(1, outer.Inner1.Val);
            Assert.AreEqual(2, outer.Inner2.Val);

            // 3. Try setting inner object in portable form.
            innerPortObj1 = _grid.GetBinary().GetBuilder(innerPortObj1).SetField("val", 3).Build();

            inner1 = innerPortObj1.Deserialize<NestedInner>();

            Assert.AreEqual(3, inner1.Val);

            outerPortObj = _grid.GetBinary().GetBuilder(outerPortObj).SetField<object>("inner1", innerPortObj1).Build();

            outer = outerPortObj.Deserialize<NestedOuter>();
            Assert.AreEqual(3, outer.Inner1.Val);
            Assert.AreEqual(2, outer.Inner2.Val);
        }

        /// <summary>
        /// Test handle migration.
        /// </summary>
        [Test]
        public void TestHandleMigration()
        {
            // 1. Simple comparison of results.
            MigrationInner inner = new MigrationInner {Val = 1};

            MigrationOuter outer = new MigrationOuter
            {
                Inner1 = inner,
                Inner2 = inner
            };

            byte[] outerBytes = _marsh.Marshal(outer);

            IBinaryObjectBuilder builder = _grid.GetBinary().GetBuilder(typeof(MigrationOuter));

            builder.SetHashCode(outer.GetHashCode());

            builder.SetField<object>("inner1", inner);
            builder.SetField<object>("inner2", inner);

            BinaryObject portOuter = (BinaryObject) builder.Build();

            byte[] portOuterBytes = new byte[outerBytes.Length];

            Buffer.BlockCopy(portOuter.Data, 0, portOuterBytes, 0, portOuterBytes.Length);

            Assert.AreEqual(outerBytes, portOuterBytes);

            // 2. Change the first inner object so that the handle must migrate.
            MigrationInner inner1 = new MigrationInner {Val = 2};

            IBinaryObject portOuterMigrated =
                _grid.GetBinary().GetBuilder(portOuter).SetField<object>("inner1", inner1).Build();

            MigrationOuter outerMigrated = portOuterMigrated.Deserialize<MigrationOuter>();

            Assert.AreEqual(2, outerMigrated.Inner1.Val);
            Assert.AreEqual(1, outerMigrated.Inner2.Val);

            // 3. Change the first value using serialized form.
            IBinaryObject inner1Port =
                _grid.GetBinary().GetBuilder(typeof(MigrationInner)).SetField("val", 2).Build();

            portOuterMigrated =
                _grid.GetBinary().GetBuilder(portOuter).SetField<object>("inner1", inner1Port).Build();

            outerMigrated = portOuterMigrated.Deserialize<MigrationOuter>();

            Assert.AreEqual(2, outerMigrated.Inner1.Val);
            Assert.AreEqual(1, outerMigrated.Inner2.Val);
        }

        /// <summary>
        /// Test handle inversion.
        /// </summary>
        [Test]
        public void TestHandleInversion()
        {
            InversionInner inner = new InversionInner();
            InversionOuter outer = new InversionOuter();

            inner.Outer = outer;
            outer.Inner = inner;

            byte[] rawOuter = _marsh.Marshal(outer);

            IBinaryObject portOuter = _marsh.Unmarshal<IBinaryObject>(rawOuter, BinaryMode.ForceBinary);
            IBinaryObject portInner = portOuter.GetField<IBinaryObject>("inner");

            // 1. Ensure that inner object can be deserialized after build.
            IBinaryObject portInnerNew = _grid.GetBinary().GetBuilder(portInner).Build();

            InversionInner innerNew = portInnerNew.Deserialize<InversionInner>();

            Assert.AreSame(innerNew, innerNew.Outer.Inner);

            // 2. Ensure that portable object with external dependencies could be added to builder.
            IBinaryObject portOuterNew =
                _grid.GetBinary().GetBuilder(typeof(InversionOuter)).SetField<object>("inner", portInner).Build();

            InversionOuter outerNew = portOuterNew.Deserialize<InversionOuter>();

            Assert.AreNotSame(outerNew, outerNew.Inner.Outer);
            Assert.AreSame(outerNew.Inner, outerNew.Inner.Outer.Inner);
        }

        /// <summary>
        /// Test build multiple objects.
        /// </summary>
        [Test]
        public void TestBuildMultiple()
        {
            IBinaryObjectBuilder builder = _grid.GetBinary().GetBuilder(typeof(Primitives));

            builder.SetField<byte>("fByte", 1).SetField("fBool", true);

            IBinaryObject po1 = builder.Build();
            IBinaryObject po2 = builder.Build();

            Assert.AreEqual(1, po1.GetField<byte>("fByte"));
            Assert.AreEqual(true, po1.GetField<bool>("fBool"));

            Assert.AreEqual(1, po2.GetField<byte>("fByte"));
            Assert.AreEqual(true, po2.GetField<bool>("fBool"));

            builder.SetField<byte>("fByte", 2);

            IBinaryObject po3 = builder.Build();

            Assert.AreEqual(1, po1.GetField<byte>("fByte"));
            Assert.AreEqual(true, po1.GetField<bool>("fBool"));

            Assert.AreEqual(1, po2.GetField<byte>("fByte"));
            Assert.AreEqual(true, po2.GetField<bool>("fBool"));

            Assert.AreEqual(2, po3.GetField<byte>("fByte"));
            Assert.AreEqual(true, po2.GetField<bool>("fBool"));

            builder = _grid.GetBinary().GetBuilder(po1);

            builder.SetField<byte>("fByte", 10);

            po1 = builder.Build();
            po2 = builder.Build();

            builder.SetField<byte>("fByte", 20);

            po3 = builder.Build();

            Assert.AreEqual(10, po1.GetField<byte>("fByte"));
            Assert.AreEqual(true, po1.GetField<bool>("fBool"));

            Assert.AreEqual(10, po2.GetField<byte>("fByte"));
            Assert.AreEqual(true, po2.GetField<bool>("fBool"));

            Assert.AreEqual(20, po3.GetField<byte>("fByte"));
            Assert.AreEqual(true, po3.GetField<bool>("fBool"));
        }

        /// <summary>
        /// Tests type id method.
        /// </summary>
        [Test]
        public void TestTypeId()
        {
            Assert.Throws<ArgumentException>(() => _grid.GetBinary().GetTypeId(null));

            Assert.AreEqual(IdMapper.TestTypeId, _grid.GetBinary().GetTypeId(IdMapper.TestTypeName));
            
            Assert.AreEqual(BinaryUtils.GetStringHashCode("someTypeName"), _grid.GetBinary().GetTypeId("someTypeName"));
        }

        /// <summary>
        /// Tests metadata methods.
        /// </summary>
        [Test]
        public void TestMetadata()
        {
            // Populate metadata
            var portables = _grid.GetBinary();

            portables.ToBinary<IBinaryObject>(new DecimalHolder());

            // All meta
            var allMetas = portables.GetBinaryTypes();

            var decimalMeta = allMetas.Single(x => x.TypeName == "DecimalHolder");

            Assert.AreEqual(new[] {"val", "valArr"}, decimalMeta.Fields);

            // By type
            decimalMeta = portables.GetBinaryType(typeof (DecimalHolder));

            Assert.AreEqual(new[] {"val", "valArr"}, decimalMeta.Fields);
            
            // By type id
            decimalMeta = portables.GetBinaryType(portables.GetTypeId("DecimalHolder"));

            Assert.AreEqual(new[] {"val", "valArr"}, decimalMeta.Fields);

            // By type name
            decimalMeta = portables.GetBinaryType("DecimalHolder");

            Assert.AreEqual(new[] {"val", "valArr"}, decimalMeta.Fields);
        }

        /// <summary>
        /// Create portable type configuration with disabled metadata.
        /// </summary>
        /// <param name="typ">Type.</param>
        /// <returns>Configuration.</returns>
        private static BinaryTypeConfiguration TypeConfigurationNoMeta(Type typ)
        {
            return new BinaryTypeConfiguration(typ);
        }
    }

    /// <summary>
    /// Empty portable class.
    /// </summary>
    public class Empty
    {
        // No-op.
    }

    /// <summary>
    /// Empty portable class with no metadata.
    /// </summary>
    public class EmptyNoMeta
    {
        // No-op.
    }

    /// <summary>
    /// Portable with primitive fields.
    /// </summary>
    public class Primitives
    {
        public byte FByte;
        public bool FBool;
        public short FShort;
        public char FChar;
        public int FInt;
        public long FLong;
        public float FFloat;
        public double FDouble;
    }

    /// <summary>
    /// Portable with primitive array fields.
    /// </summary>
    public class PrimitiveArrays
    {
        public byte[] FByte;
        public bool[] FBool;
        public short[] FShort;
        public char[] FChar;
        public int[] FInt;
        public long[] FLong;
        public float[] FFloat;
        public double[] FDouble;
    }

    /// <summary>
    /// Portable having strings, dates, Guids and enums.
    /// </summary>
    public class StringDateGuidEnum
    {
        public string FStr;
        public DateTime? FnDate;
        public Guid? FnGuid;
        public TestEnum FEnum;

        public string[] FStrArr;
        public DateTime?[] FDateArr;
        public Guid?[] FGuidArr;
        public TestEnum[] FEnumArr;
    }

    /// <summary>
    /// Enumeration.
    /// </summary>
    public enum TestEnum
    {
        One, Two
    }

    /// <summary>
    /// Portable with raw data.
    /// </summary>
    public class WithRaw : IBinarizable
    {
        public int A;
        public int B;

        /** <inheritDoc /> */
        public void WriteBinary(IBinaryWriter writer)
        {
            writer.WriteInt("a", A);
            writer.GetRawWriter().WriteInt(B);
        }

        /** <inheritDoc /> */
        public void ReadBinary(IBinaryReader reader)
        {
            A = reader.ReadInt("a");
            B = reader.GetRawReader().ReadInt();
        }
    }

    /// <summary>
    /// Empty class for metadata overwrite test.
    /// </summary>
    public class MetaOverwrite
    {
        // No-op.
    }

    /// <summary>
    /// Nested outer object.
    /// </summary>
    public class NestedOuter
    {
        public NestedInner Inner1;
        public NestedInner Inner2;
    }

    /// <summary>
    /// Nested inner object.
    /// </summary>
    public class NestedInner
    {
        public int Val;
    }

    /// <summary>
    /// Outer object for handle migration test.
    /// </summary>
    public class MigrationOuter
    {
        public MigrationInner Inner1;
        public MigrationInner Inner2;
    }

    /// <summary>
    /// Inner object for handle migration test.
    /// </summary>
    public class MigrationInner
    {
        public int Val;
    }

    /// <summary>
    /// Outer object for handle inversion test.
    /// </summary>
    public class InversionOuter
    {
        public InversionInner Inner;
    }

    /// <summary>
    /// Inner object for handle inversion test.
    /// </summary>
    public class InversionInner
    {
        public InversionOuter Outer;
    }

    /// <summary>
    /// Object for composite array tests.
    /// </summary>
    public class CompositeArray
    {
        public object[] InArr;
        public object[] OutArr;
    }

    /// <summary>
    /// Object for composite collection/dictionary tests.
    /// </summary>
    public class CompositeContainer
    {
        public ICollection Col;
        public IDictionary Dict;
    }

    /// <summary>
    /// OUter object for composite structures test.
    /// </summary>
    public class CompositeOuter
    {
        public CompositeInner Inner;

        public CompositeOuter()
        {
            // No-op.
        }

        public CompositeOuter(CompositeInner inner)
        {
            Inner = inner;
        }
    }

    /// <summary>
    /// Inner object for composite structures test.
    /// </summary>
    public class CompositeInner
    {
        public int Val;

        public CompositeInner()
        {
            // No-op.
        }

        public CompositeInner(int val)
        {
            Val = val;
        }
    }

    /// <summary>
    /// Type to test "ToPortable()" logic.
    /// </summary>
    public class ToPortable
    {
        public int Val;

        public ToPortable(int val)
        {
            Val = val;
        }
    }

    /// <summary>
    /// Type to test "ToPortable()" logic with metadata disabled.
    /// </summary>
    public class ToPortableNoMeta
    {
        public int Val;

        public ToPortableNoMeta(int val)
        {
            Val = val;
        }
    }

    /// <summary>
    /// Type to test removal.
    /// </summary>
    public class Remove
    {
        public object Val;
        public RemoveInner Val2;
    }

    /// <summary>
    /// Inner type to test removal.
    /// </summary>
    public class RemoveInner
    {
        /** */
        public int Val;

        /// <summary>
        ///
        /// </summary>
        /// <param name="val"></param>
        public RemoveInner(int val)
        {
            Val = val;
        }
    }

    /// <summary>
    ///
    /// </summary>
    public class BuilderInBuilderOuter
    {
        /** */
        public BuilderInBuilderInner Inner;

        /** */
        public BuilderInBuilderInner Inner2;
    }

    /// <summary>
    ///
    /// </summary>
    public class BuilderInBuilderInner
    {
        /** */
        public BuilderInBuilderOuter Outer;
    }

    /// <summary>
    ///
    /// </summary>
    public class BuilderCollection
    {
        /** */
        public readonly ArrayList Col;

        /// <summary>
        ///
        /// </summary>
        /// <param name="col"></param>
        public BuilderCollection(ArrayList col)
        {
            Col = col;
        }
    }

    /// <summary>
    ///
    /// </summary>
    public class BuilderCollectionItem
    {
        /** */
        public int Val;

        /// <summary>
        ///
        /// </summary>
        /// <param name="val"></param>
        public BuilderCollectionItem(int val)
        {
            Val = val;
        }
    }

    /// <summary>
    ///
    /// </summary>
    public class DecimalHolder
    {
        /** */
        public decimal Val;

        /** */
        public decimal?[] ValArr;
    }

    /// <summary>
    /// Test id mapper.
    /// </summary>
    public class IdMapper : IBinaryIdMapper
    {
        /** */
        public const string TestTypeName = "IdMapperTestType";

        /** */
        public const int TestTypeId = -65537;

        /** <inheritdoc /> */
        public int GetTypeId(string typeName)
        {
            return typeName == TestTypeName ? TestTypeId : 0;
        }

        /** <inheritdoc /> */
        public int GetFieldId(int typeId, string fieldName)
        {
            return 0;
        }
    }
}