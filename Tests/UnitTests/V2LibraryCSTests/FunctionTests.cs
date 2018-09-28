﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CNTK.V2LibraryCSTests
{
    [TestClass]
    public class FunctionTests
    {
        [TestMethod]
        public void TestSaveAndLoad()
        {
            int channels = 2;
            int imageWidth = 40, imageHeight = 40;
            int[] inputDim = { imageHeight, imageWidth, channels };
            Variable input = CNTKLib.InputVariable(inputDim, DataType.Float, "Images");
            Parameter param = new Parameter(inputDim, DataType.Float, CNTKLib.GlorotUniformInitializer(0.1F, 1, 0), DeviceDescriptor.CPUDevice);
            Function model = CNTKLib.Plus(input, param, "Plus");
            byte[] buffer = model.Save();
            Function loadedModel = Function.Load(buffer, DeviceDescriptor.CPUDevice);
            Assert.AreEqual(model.Name, loadedModel.Name);
            Assert.AreEqual(model.Inputs.Count, loadedModel.Inputs.Count);
            Assert.AreEqual(model.Inputs[0].Shape, loadedModel.Inputs[0].Shape);
            Assert.AreEqual(model.Output.Shape, loadedModel.Output.Shape);
        }

        [TestMethod]
        public void TestSaveAndLoadToFile()
        {
            int channels = 2;
            int imageWidth = 40, imageHeight = 40;
            int[] inputDim = { imageHeight, imageWidth, channels };
            Variable input = CNTKLib.InputVariable(inputDim, DataType.Float, "Images");
            Parameter param = new Parameter(inputDim, DataType.Float, CNTKLib.GlorotUniformInitializer(0.1F, 1, 0), DeviceDescriptor.CPUDevice);
            Function model = CNTKLib.Plus(input, param, "Minus");
            string savedFileName = "./TestSaveAndLoadToFileSavedModel.txt";
            model.Save(savedFileName);
            byte[] buffer = model.Save();
            Function loadedModel = Function.Load(savedFileName, DeviceDescriptor.CPUDevice);
            File.Delete(savedFileName);
            Assert.AreEqual(model.Name, loadedModel.Name);
            Assert.AreEqual(model.Inputs.Count, loadedModel.Inputs.Count);
            Assert.AreEqual(model.Inputs[0].Shape, loadedModel.Inputs[0].Shape);
            Assert.AreEqual(model.Output.Shape, loadedModel.Output.Shape);
        }

        [TestMethod]
        public void TestSetAndGetRandomSeed()
        {
            uint expectedRandomSeed = 20;
            uint randomSeed = expectedRandomSeed;
            CNTKLib.SetFixedRandomSeed(randomSeed);
            var isSeedFixed = CNTKLib.IsRandomSeedFixed();
            var retrievedRandomSeed = CNTKLib.GetRandomSeed();
            Assert.AreEqual(true, isSeedFixed);
            Assert.AreEqual(expectedRandomSeed, retrievedRandomSeed);
        }

        [TestMethod]
        public void TestForceDeterministicAlgorithms()
        {
            CNTKLib.ForceDeterministicAlgorithms();
            var shouldForce = CNTKLib.ShouldForceDeterministicAlgorithms();
            Assert.AreEqual(true, shouldForce);
        }

        [TestMethod]
        public void TestMemoryLiveness()
        {
            const int dim = 1000;
            var device = DeviceDescriptor.UseDefaultDevice();

            // NoOp function using alias: Only used to read memory through C++
            var inputShape = NDShape.CreateNDShape(new[] {dim});
            var input = CNTKLib.InputVariable(inputShape, DataType.Float);
            var f = CNTKLib.Alias(input);

            // Create objects in another thread to make the error more likely
            var t = new Thread(() => {
                object[] objs = new object[100];
                int i = 0;
                while (true)
                {
                    objs[i] = new object();
                    i = (i + 1) % objs.Length;
                }
            });

            t.Start();

            for (int i = 0; i< 1000; i++)
            {
                // Zero vector that lives longer than it's used
                var data = new float[dim];

                var dataShape = NDShape.CreateNDShape(new[] { dim, 1 });
                var arrayView = new NDArrayView(dataShape, data, device);
                var inputValue = new Value(arrayView);

                // Force GC to trigger error
                GC.Collect();
                GC.WaitForPendingFinalizers();

                var outputs = new Dictionary<Variable, Value>
                {
                    {f.Output, null }
                };

                f.Evaluate(new Dictionary<Variable, Value>
                {
                    {input, inputValue}
                }, outputs, device);

                var outputValue = outputs.Values.Single();
                var outputData = outputValue.GetDenseData<float>(f.Output);

                // Verify that the original data has not changed
                Assert.IsTrue(data.All(x => Math.Abs(x - 0.0) < float.Epsilon), "Data has changed");

                // Error if any value is not zero since the output should be the input (zero vector)
                Assert.IsTrue(outputData.SelectMany(x => x).Any(x => Math.Abs(x-0.0) < float.Epsilon), "Output doesn't equal to input");
            }
        }

    }
}
