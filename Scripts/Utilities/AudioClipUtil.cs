﻿/**
* Copyright 2015 IBM Corp. All Rights Reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
*      http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*
*/

using IBM.Watson.DeveloperCloud.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IBM.Watson.DeveloperCloud.Utilities
{
    /// <summary>
    /// AudioClip helper functions.
    /// </summary>
    public static class AudioClipUtil
    {
        /// <summary>
        /// This function will combine any number of AudioClips into a single AudioClip. The clips must be the same number of channels
        /// and frequency.
        /// </summary>
        /// <param name="clips">Variable number of AudioClip objects may be provided.</param>
        /// <returns>Returns the resulting AudioClip.</returns>
        public static AudioClip Combine(params AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
                return null;

            AudioClip firstClip = null;

            int length = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                    continue;

                if ( firstClip != null )
                {
                    if ( firstClip.channels != clips[i].channels 
                        || firstClip.frequency != clips[i].frequency )
                    {
                        Log.Error( "AudioClipUtil", "Combine() requires clips to have the sample number of channels and same frequency." );
                        return null;
                    }
                }
                else
                    firstClip = clips[i];

                length += clips[i].samples * clips[i].channels;
            }

            float[] data = new float[length];
            length = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                    continue;

                float[] buffer = new float[clips[i].samples * clips[i].channels];
                clips[i].GetData(buffer, 0);
                buffer.CopyTo(data, length);
                length += buffer.Length;
            }

            if (length == 0)
                return null;

            AudioClip result = AudioClip.Create( firstClip.name, length / firstClip.channels, firstClip.channels, firstClip.frequency, false );
            result.SetData(data, 0);

            return result;
        }

        /// <summary>
        /// Returns linear 16-bit audio data for the given AudioClip object.
        /// </summary>
        /// <param name="clip">The AudioClip object.</param>
        /// <returns>A byte array of 16-bit audio data.</returns>
        public static byte[] GetL16(AudioClip clip)
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            float divisor = (1 << 15);
            for (int i = 0; i < samples.Length; ++i)
                writer.Write((short)(samples[i] * divisor));

            byte[] data = new byte[samples.Length * 2];
            Array.Copy(stream.GetBuffer(), data, data.Length);

            return data;
        }

        private static Queue<AudioClip> sm_DestroyQueue = new Queue<AudioClip>();
        private static int sm_DestroyQueueID = 0;

        /// <summary>
        /// Returns the state of the AudioClip destroy queue.
        /// </summary>
        /// <returns>Returns true if the destoy queue processor is active.</returns>
        public static bool IsDestroyQueueActive()
        {
            return sm_DestroyQueueID != 0;
        }

        /// <summary>
        /// Start up the AudioClip destroy queue processor.
        /// </summary>
        public static void StartDestroyQueue()
        {
            if ( sm_DestroyQueueID == 0 )
                sm_DestroyQueueID = Runnable.Run( ProcessDestroyQueue() );
        }

        /// <summary>
        /// Stop the AudioClip destroy processor.
        /// </summary>
        public static void StopDestroyQueue()
        {
            if ( sm_DestroyQueueID != 0 )
            {
                Runnable.Stop( sm_DestroyQueueID );
                sm_DestroyQueueID = 0;
            }
        }

        /// <summary>
        /// Queue an AudioClip for destruction on the main thread. This function is thread-safe.
        /// </summary>
        /// <param name="clip">The AudioClip to destroy.</param>
        public static void DestroyAudioClip( AudioClip clip )
        {
            if ( sm_DestroyQueueID == 0 )
                    throw new WatsonException( "AudioClip destroy queue not started." );

            lock( sm_DestroyQueue )
                sm_DestroyQueue.Enqueue( clip );
        }

        private static IEnumerator ProcessDestroyQueue()
        {
            yield return null;

            while( sm_DestroyQueueID != 0 )
            {
                yield return new WaitForSeconds( 1.0f );

                lock( sm_DestroyQueue )
                {
                    while( sm_DestroyQueue.Count > 0 )
                    {
                        AudioClip clip = sm_DestroyQueue.Dequeue();
                        UnityEngine.Object.DestroyImmediate( clip, true );
                    }
                }
            }
        }
    }
}