﻿namespace Microsoft.Threading {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.Remoting.Messaging;
	using System.Text;
	using System.Threading.Tasks;

	/// <summary>
	/// Stores reference types in the CallContext such that marshaling is safe.
	/// </summary>
	/// <typeparam name="T">The type of value to store.</typeparam>
	public class AsyncLocal<T> where T : class {
		private readonly object syncObject = new object();

		/// <summary>
		/// A weak reference table that associates simple objects with some specific type that cannot be marshaled.
		/// </summary>
		private readonly ConditionalWeakTable<object, T> valueTable = new ConditionalWeakTable<object, T>();

		/// <summary>
		/// A table that is used to look up a previously stored simple object to represent a given value.
		/// </summary>
		/// <remarks>
		/// This is just an optimization. We could totally remove this field and all use of it and the tests still pass,
		/// amazingly enough.
		/// </remarks>
		private readonly ConditionalWeakTable<T, object> reverseLookupTable = new ConditionalWeakTable<T, object>();

		/// <summary>
		/// A unique GUID that prevents this instance from conflicting with other instances.
		/// </summary>
		private readonly string callContextKey = Guid.NewGuid().ToString();

		/// <summary>
		/// Gets or sets the value to associate with the current CallContext.
		/// </summary>
		public T Value {
			get {
				object boxKey = CallContext.LogicalGetData(this.callContextKey);
				T value;
				if (boxKey != null) {
					lock (this.syncObject) {
						if (this.valueTable.TryGetValue(boxKey, out value)) {
							return value;
						}
					}
				}

				return null;
			}

			set {
				if (value != null) {
					lock (this.syncObject) {
						object callContextValue;
						if (!this.reverseLookupTable.TryGetValue(value, out callContextValue)) {
							// Use a MarshalByRefObject for the value so it doesn't
							// lose reference identity across appdomain transitions.
							callContextValue = new IdentityNode();
							this.reverseLookupTable.Add(value, callContextValue);
						}

						CallContext.LogicalSetData(this.callContextKey, callContextValue);
						this.valueTable.Remove(callContextValue);
						this.valueTable.Add(callContextValue, value);
					}
				} else {
					CallContext.FreeNamedDataSlot(this.callContextKey);
				}
			}
		}

		/// <summary>
		/// A simple marshalable object that can retain identity across app domain transitions.
		/// </summary>
		private class IdentityNode : MarshalByRefObject {
		}
	}
}
