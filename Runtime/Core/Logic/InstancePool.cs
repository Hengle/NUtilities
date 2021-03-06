﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace Vertx
{
	/// <summary>
	/// A pool for Component instances.
	/// </summary>
	/// <typeparam name="TInstanceType">The Component Type associated with the pool</typeparam>
	public static class InstancePool<TInstanceType> where TInstanceType : Component
	{
		/// <summary>
		/// Dictionary of prefab components to stacks of pooled instances.
		/// </summary>
		private static readonly Dictionary<TInstanceType, Stack<TInstanceType>> pool = new Dictionary<TInstanceType, Stack<TInstanceType>>();

		/// <summary>
		/// Retrieves an instance from the pool, positioned at the origin.
		/// </summary>
		/// <param name="prefab">The prefab key to retrieve instances of.</param>
		/// <param name="parent">The parent to parent instances under.</param>
		/// <returns>An instance retrieved from the pool.</returns>
		public static TInstanceType Get(TInstanceType prefab, Transform parent = null)
			=> Get(prefab, parent, Vector3.zero, Quaternion.identity);

		/// <summary>
		/// Retrieves a positioned instance from the pool.
		/// </summary>
		/// <param name="prefab">The prefab key to retrieve instances of.</param>
		/// <param name="parent">The parent to parent instances under.</param>
		/// <param name="position">Position of the instance</param>
		/// <param name="rotation">Rotation of the instance</param>
		/// <param name="space">Which space the position and rotation is applied in</param>
		/// <returns>An instance retrieved from the pool.</returns>
		public static TInstanceType Get(TInstanceType prefab, Transform parent, Vector3 position, Quaternion rotation, Space space = Space.World)
		{
			Assert.IsNotNull(prefab, $"Prefab passed to InstancePool<{typeof(TInstanceType).Name}>{nameof(Get)} was null");
			
			// Use the pool if we have one already
			if (pool.TryGetValue(prefab, out var stack))
			{
				if (stack.Count > 0)
				{
					TInstanceType poppedInstance;
					bool found;
					do
					{
						//Iterate to remove null items from the stack
						poppedInstance = stack.Pop();
						found = poppedInstance != null;
					} while (!found && stack.Count > 0);

					if (found)
					{
						// Activate and re-parent
						poppedInstance.gameObject.SetActive(true);
						Transform t = poppedInstance.transform;
						if (t.parent != parent)
							t.SetParent(parent);
						
						//Position
						switch (space)
						{
							case Space.World:
								t.SetPositionAndRotation(position, rotation);
								break;
							case Space.Self:
								t.localPosition = position;
								t.localRotation = rotation;
								break;
							default:
								throw new ArgumentOutOfRangeException(nameof(space), space, null);
						}

						return poppedInstance;
					}
				}
			}

			// Otherwise return a new instance.
			// Only when an instance is returned do we need to create a pool.
			TInstanceType instance;
			// Position
			switch (space)
			{
				case Space.World:
					instance = Object.Instantiate(prefab, position, rotation, parent);
					break;
				case Space.Self:
					instance = Object.Instantiate(prefab, parent);
					Transform t = instance.transform;
					t.localPosition = position;
					t.localRotation = rotation;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(space), space, null);
			}

			return instance;
		}

		/// <summary>
		/// Returns a Component instance to the pool.
		/// </summary>
		/// <param name="prefab">The prefab key used when the instance was retrieved via <see cref="Get(TInstanceType,UnityEngine.Transform)"/></param>
		/// <param name="instance">The instance to return to the pool.</param>
		public static void Pool(TInstanceType prefab, TInstanceType instance)
		{
			// Create a pool if we don't have one already.
			if (!pool.TryGetValue(prefab, out var stack))
			{
				stack = new Stack<TInstanceType>();
				pool.Add(prefab, stack);
			}

			// Disable the object and push it to the stack.
			instance.gameObject.SetActive(false);
			stack.Push(instance);
		}

		/// <summary>
		/// If you are temporarily working with pools for prefabs you can remove them from the system by calling this function.
		/// </summary>
		/// <param name="prefab">The prefab key referring to the pool.</param>
		public static void RemovePrefabPool(TInstanceType prefab)
		{
			if (pool.ContainsKey(prefab))
				pool.Remove(prefab);
		}
	}
}