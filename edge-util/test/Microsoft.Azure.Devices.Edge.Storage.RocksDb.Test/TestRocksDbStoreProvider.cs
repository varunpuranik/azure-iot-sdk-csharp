// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb;

    public class TestRocksDbStoreProvider : IDisposable
    {
        private string rocksDbFolder;
        private DbStoreProvider rocksDbStoreProvider;

        public TestRocksDbStoreProvider()
        {
            string tempFolder = Path.GetTempPath();
            this.rocksDbFolder = Path.Combine(tempFolder, $"edgeTestDb{Guid.NewGuid()}");
            if (Directory.Exists(this.rocksDbFolder))
            {
                Directory.Delete(this.rocksDbFolder);
            }
            Directory.CreateDirectory(this.rocksDbFolder);
            this.rocksDbStoreProvider = DbStoreProvider.Create(this.rocksDbFolder, new string[0]);
        }

        public IDbStore GetColumnStoreFamily(string columnFamilyName) =>
            this.rocksDbStoreProvider.GetDbStore(columnFamilyName);

        public void Dispose()
        {
            this.rocksDbStoreProvider?.Dispose();
            if (!string.IsNullOrWhiteSpace(this.rocksDbFolder) && Directory.Exists(this.rocksDbFolder))
            {
                Directory.Delete(this.rocksDbFolder, true);
            }
        }
    }
}