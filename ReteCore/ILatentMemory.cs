//-----------------------------------------------------------------------
// <copyright file="ILatentMemory.cs">
//     Copyright (c) Steven Orton. All rights reserved.
//     Licensed under the GNU Lesser General Public License v2.1.
//     See LICENSE file in the ReteRaven project root for full license
//     information.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReteCore
{
    /// <summary>
    /// An interface representing a latent memory in a Rete network, which holds tokens that are not yet 
    /// fully processed or activated.
    /// </summary>
    public interface ILatentMemory
    {
        /// <summary>
        /// The collection of tokens that are currently stored in this latent memory. Each token represents a 
        /// combination of facts that have matched certain conditions in the Rete network but have not yet 
        /// been fully processed or activated.
        /// </summary>
        public IEnumerable<Token> Tokens { get; }
    }
}
