﻿using System;
using Rebus.Config;
using Rebus.Subscriptions;

namespace Rebus.Persistence.InMem
{
    /// <summary>
    /// Configuration extensions for in-mem subscriptionstorage
    /// </summary>
    public static class InMemorySubscriptionStorageExtensions
    {
        /// <summary>
        /// Configures Rebus to store subscriptions in memory. The subscription storage is assumed to be CENTRALIZED
        /// with this overload because a <see cref="InMemorySubscriberStore"/> is passed in.  PLEASE NOTE that this 
        /// is probably not useful for any other scenario  than TESTING because usually you want subscriptions to be PERSISTENT.
        /// </summary>
        public static void StoreInMemory(this StandardConfigurer<ISubscriptionStorage> configurer, InMemorySubscriberStore inMemorySubscriberStore)
        {
            if (inMemorySubscriberStore == null) throw new ArgumentNullException(nameof(inMemorySubscriberStore));
            configurer.Register(c => new InMemorySubscriptionStorage(inMemorySubscriberStore));
        }

        /// <summary>
        /// Configures Rebus to store subscriptions in memory. The subscription storage is assumed to be DECENTRALIZED
        /// with this overload because NO <see cref="InMemorySubscriberStore"/> is passed in and subscriptions are therefore private
        /// for this endpoint.  PLEASE NOTE that this  is probably not useful for any other scenario  than TESTING because usually you want 
        /// subscriptions to be PERSISTENT.
        /// </summary>
        public static void StoreInMemory(this StandardConfigurer<ISubscriptionStorage> configurer)
        {
            configurer.Register(c => new InMemorySubscriptionStorage());
        }
    }
}