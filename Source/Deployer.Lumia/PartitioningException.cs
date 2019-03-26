using System;

namespace Deployer.Lumia
{
    internal class PartitioningException : ApplicationException
    {
        public PartitioningException(string message) : base(message)
        {
        }
    }
}