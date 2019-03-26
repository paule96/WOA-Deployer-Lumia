using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Deployer.FileSystem;
using Deployer.FileSystem.Gpt;
using Serilog;
using Zafiro.Core;
using Partition = Deployer.FileSystem.Partition;

namespace Deployer.Lumia
{
    public class PartitionCleaner : IPartitionCleaner
    {
        private IPhone phone;
        private Partition dataPartition;
        private Disk disk;

        public async Task Clean(IPhone toClean)
        {
            Log.Information("Performing partition cleanup");

            phone = toClean;

            disk = await toClean.GetDeviceDisk();
            var dataVolume = await phone.GetDataVolume();
            dataPartition = dataVolume?.Partition;


            PerformPartitionCleanupByName();
            await RemoveAnyPartitionsAfterData();

            Log.Verbose("Refreshing disk");

            await EnsureDataIsLastPartition();

            await disk.Refresh();
        }

        private async Task EnsureDataIsLastPartition()
        {
            using (var c = new GptContext(disk.Number, FileAccess.Read))
            {
                var last = c.Partitions.Last();
                var asCommon = last.AsCommon(disk);
                var volume = await asCommon.GetVolume();
                if (volume.Label != "Data")
                {
                    throw new PartitioningException("Data should be the last volume after a the cleanup");
                }
            }
        }

        private void PerformPartitionCleanupByName()
        {
            Log.Verbose("Removing existing partitions by name");

            using (var c = new GptContext(disk.Number, FileAccess.ReadWrite))
            {
                c.RemoveExisting(PartitionName.System);
                c.RemoveExisting(PartitionName.Reserved);
                c.RemoveExisting(PartitionName.Windows);
            }
        }

        private async Task RemoveAnyPartitionsAfterData()
        {
            if (dataPartition == null)
            {
                Log.Verbose("Data partition not found. The removal of partitions after Data won't be performed");
                return;
            }

            Log.Verbose("Trying to remove partitions created by previous versions of WOA Deployer");

            var partNumber = (int)dataPartition.Number;
            var partitions = await disk.GetPartitions();

            var toRemove = partitions
                .Skip(partNumber)
                .ToList();

            Log.Verbose("Removing legacy partitions");
            foreach (var partition in toRemove)
            {
                await partition.Remove();
            }

            await EnsurePartitionsAreRemoved(toRemove);
        }

        private async Task EnsurePartitionsAreRemoved(ICollection<Partition> toRemove)
        {
            var existing = await disk.GetPartitions();
            if (toRemove.IsSubsetOf(existing))
            {
                throw new PartitioningException("Couldn't remove all the partitions during the cleanup.");
            }
        }
    }
}