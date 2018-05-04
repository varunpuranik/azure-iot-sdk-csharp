// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    public abstract class SequentialStoreTestBase
    {
        protected abstract IEntityStore<byte[], TV> GetEntityStore<TV>(string entityName);

        [Fact]
        public async Task CreateTest()
        {
            IEntityStore<byte[], Item> entityStore = this.GetEntityStore<Item>("testEntity");
            ISequentialStore<Item> sequentialStore = await SequentialStore<Item>.Create(entityStore);
            long offset = await sequentialStore.Append(new Item { Prop1 = 10 });
            Assert.Equal(0, offset);

            sequentialStore = await SequentialStore<Item>.Create(entityStore);
            offset = await sequentialStore.Append(new Item { Prop1 = 20 });
            Assert.Equal(1, offset);
        }

        [Fact]
        public async Task BasicTest()
        {            
            ISequentialStore<Item> sequentialStore = await this.GetSequentialStore("basicTest");
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                int i1 = i;
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        long offset = await sequentialStore.Append(new Item { Prop1 = i1 * 10 + j });
                        Assert.True(offset <= 10000);
                    }
                }));
            }
            await Task.WhenAll(tasks);

            IEnumerable<(long offset, Item item)> batch = await sequentialStore.GetBatch(0, 10000);
            IEnumerable<(long offset, Item item)> batchItems = batch as IList<(long offset, Item item)> ?? batch.ToList();
            Assert.Equal(10000, batchItems.Count());
            int counter = 0;
            foreach ((long offset, Item item) batchItem in batchItems)
            {
                Assert.Equal(counter++, batchItem.offset);
            }
        }

        [Fact]
        public async Task RemoveTest()
        {
            ISequentialStore<Item> sequentialStore = await this.GetSequentialStore("removeTestEntity");
            for (int i = 0; i < 10; i++)
            {
                long offset = await sequentialStore.Append(new Item { Prop1 = i });
                Assert.Equal(i, offset);
            }

            IEnumerable<(long offset, Item item)> batch = await sequentialStore.GetBatch(0, 100);
            IEnumerable<(long offset, Item item)> batchItemsAsList = batch as IList<(long offset, Item item)> ?? batch.ToList();
            Assert.Equal(10, batchItemsAsList.Count());
            int counter = 0;
            foreach ((long offset, Item item) batchItem in batchItemsAsList)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 0));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sequentialStore.GetBatch(0, 100));

            batch = await sequentialStore.GetBatch(1, 100);
            IEnumerable<(long offset, Item item)> items = batch as IList<(long offset, Item item)> ?? batch.ToList();
            Assert.Equal(9, items.Count());
            counter = 1;
            foreach ((long offset, Item item) batchItem in items)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 0));
            batch = await sequentialStore.GetBatch(1, 100);
            IEnumerable<(long offset, Item item)> batchItems = batch as IList<(long offset, Item item)> ?? batch.ToList();
            Assert.Equal(9, batchItems.Count());
            counter = 1;
            foreach ((long offset, Item item) batchItem in batchItems)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 1));
            batch = await sequentialStore.GetBatch(2, 100);
            IEnumerable<(long offset, Item item)> batchAsList = batch as IList<(long offset, Item item)> ?? batch.ToList();
            Assert.Equal(8, batchAsList.Count());
            counter = 2;
            foreach ((long offset, Item item) batchItem in batchAsList)
            {
                Assert.Equal(counter++, batchItem.offset);
            }
        }

        [Fact]
        public async Task GetBatchInvalidInputTest()
        {
            // Arrange
            
            ISequentialStore<Item> sequentialStore = await this.GetSequentialStore("invalidGetBatch");

            // Try to get the batch, should return empty batch. 
            List<(long, Item)> batch = (await sequentialStore.GetBatch(0, 10)).ToList();
            Assert.NotNull(batch);
            Assert.Equal(0, batch.Count);

            // Add 10 elements and remove 4, so that the range of elements is 4 - 9
            for (int i = 0; i < 10; i++)
            {
                long offset = await sequentialStore.Append(new Item { Prop1 = i });
                Assert.Equal(i, offset);
            }

            for (int i = 0; i < 4; i++)
            {
                await sequentialStore.RemoveFirst((o, itm) => Task.FromResult(true));
            }

            // Try to get with starting offset < 4, should throw. 
            for (int i = 0; i < 4; i++)
            {
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sequentialStore.GetBatch(i, 10));
            }

            // Try to get with starting offset between 4 and 9, should return a valid batch. 
            for (int i = 4; i < 10; i++)
            {
                batch = (await sequentialStore.GetBatch(i, 10)).ToList();
                Assert.NotNull(batch);
                Assert.Equal(10 - i, batch.Count);
                Assert.Equal(i, batch[0].Item1);
            }

            // Try to get with starting offset > 10, should return empty batch
            for (int i = 10; i < 14; i++)
            {
                batch = (await sequentialStore.GetBatch(i, 10)).ToList();
                Assert.NotNull(batch);
                Assert.Equal(0, batch.Count);
            }            
        }

        async Task<ISequentialStore<Item>> GetSequentialStore(string entityName)
        {
            IEntityStore<byte[], Item> entityStore = this.GetEntityStore<Item>(entityName);
            ISequentialStore<Item> sequentialStore = await SequentialStore<Item>.Create(entityStore);
            return sequentialStore;
        }

        public class Item
        {
            public long Prop1 { get; set; }
        }
    }
}