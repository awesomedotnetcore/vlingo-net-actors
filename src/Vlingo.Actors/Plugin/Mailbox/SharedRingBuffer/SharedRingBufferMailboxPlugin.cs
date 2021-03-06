﻿// Copyright (c) 2012-2018 Vaughn Vernon. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using System.Collections.Concurrent;

namespace Vlingo.Actors.Plugin.Mailbox.SharedRingBuffer
{
    public class SharedRingBufferMailboxPlugin : AbstractPlugin, IMailboxProvider
    {
        private readonly SharedRingBufferMailboxPluginConfiguration configuration;
        private readonly ConcurrentDictionary<int, RingBufferDispatcher> dispatchers;

        public SharedRingBufferMailboxPlugin()
        {
            configuration = SharedRingBufferMailboxPluginConfiguration.Define();
            dispatchers = new ConcurrentDictionary<int, RingBufferDispatcher>(16, 1);
        }

        public override string Name => configuration.Name;

        public override int Pass => 1;

        public override IPluginConfiguration Configuration => configuration;

        public override void Close()
            => dispatchers.Values.ToList().ForEach(x => x.Close());

        public override void Start(IRegistrar registrar)
        {
            registrar.Register(configuration.Name, configuration.IsDefaultMailbox, this);
        }

        public IMailbox ProvideMailboxFor(int hashCode) => ProvideMailboxFor(hashCode, null);

        public IMailbox ProvideMailboxFor(int hashCode, IDispatcher dispatcher)
        {
            RingBufferDispatcher maybeDispatcher = null;

            if (dispatcher != null)
            {
                maybeDispatcher = (RingBufferDispatcher)dispatcher;
            }
            else
            {
                dispatchers.TryGetValue(hashCode, out maybeDispatcher);
            }

            if (maybeDispatcher == null)
            {
                var newDispatcher = new RingBufferDispatcher(
                    configuration.RingSize,
                    configuration.FixedBackoff,
                    configuration.DispatcherThrottlingCount);

                var otherDispatcher = dispatchers.GetOrAdd(hashCode, newDispatcher);

                if (otherDispatcher != null)
                {
                    otherDispatcher.Start();
                    return otherDispatcher.Mailbox;
                }
                else
                {
                    newDispatcher.Start();
                    return newDispatcher.Mailbox;
                }
            }

            return maybeDispatcher.Mailbox;
        }
    }
}
