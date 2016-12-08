﻿//-----------------------------------------------------------------------
// <copyright file="FileContainer.cs" company="none">
// Copyright (C) 2013
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by 
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful, 
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details. 
//
//   You should have received a copy of the GNU General Public License
//   along with this program.  If not, see "http://www.gnu.org/licenses/". 
// </copyright>
// <author>pleoNeX</author>
// <email>benito356@gmail.com</email>
// <date>11/06/2013</date>
//-----------------------------------------------------------------------
namespace Libgame
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    
    /// <summary>
    /// Description of FileContainer.
    /// </summary>
    public abstract class FileContainer
    {    
        private string name;
          
        private FileContainer previousContainer;
        private List<FileContainer> files;
        private List<FileContainer> folders;
        private Dictionary<string, object> tags = new Dictionary<string, object>();
        
        /// <summary>
        /// Initializes a new instance of the <see cref="GameFolder" /> class.
        /// </summary>
        /// <param name="name">Folder name</param>
        /// <param name="id">Folder ID</param>
        public FileContainer(string name)
        {
            this.Path = PathSeparator + name;    // Until is added to a file container is a root container
            this.Name = name;
            
            this.files   = new List<FileContainer>();
            this.folders = new List<FileContainer>();
        }
        
        #region Properties
        
        /// <summary>
        /// Gets the path separator.
        /// </summary>
        /// <value>The path separator.</value>
        public static char PathSeparator {
            get { return '/'; }
        }

        /// <summary>
        /// Gets the folder name.
        /// </summary>
        public string Name
        {
            get { return this.name; }
            set {
                this.name = value;

                // Update path
                this.Path = this.Path.GetPreviousPath() + PathSeparator + this.name;
            }
        }
        
        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        /// <value>The path.</value>
        public string Path {
            get;
            private set;
        }

        /// <summary>
        /// Gets the list of files.
        /// </summary>
        public FileContainerCollection Files 
        {
            get { return new FileContainerCollection(this.files); }
        }
        
        /// <summary>
        /// Gets the list of folders.
        /// </summary>
        public FileContainerCollection Folders
        {
            get { return new FileContainerCollection(this.folders); }
        }
        
        /// <summary>
        /// Gets the FileContainers that contains this instance.
        /// </summary>
        /// <value>The previous file container.</value>
        public FileContainer PreviousContainer 
        {
            get { return this.previousContainer; }
        }

        /// <summary>
        /// Gets the tags of this container.
        /// </summary>
        /// <value>The tags.</value>
        public IDictionary<string, object> Tags {
            get { return this.tags; }
        }

        #endregion
        
        /// <summary>
        /// Add a file to the folder.
        /// </summary>
        /// <param name="file">File to add.</param>
        public void AddFile(FileContainer file)
        {
            this.AddContainer(file, this.files);
        }
        
        /// <summary>
        /// Add a list of files to the folder.
        /// </summary>
        /// <param name="files">List of files to add.</param>
        public void AddFiles(IEnumerable<FileContainer> files)
        {
            foreach (FileContainer file in files) {
                this.AddFile(file);
            }
        }
       
        /// <summary>
        /// Add a subfolder to the folder.
        /// </summary>
        /// <param name="folder">Folder to add.</param>
        public void AddFolder(FileContainer folder)
        {
            this.AddContainer(folder, this.folders);
        }

        /// <summary>
        /// Add a list of subfolders to the folder.
        /// </summary>
        /// <param name="folders">List of folders to add.</param>
        public void AddFolders(IEnumerable<FileContainer> folders)
        {
            foreach (FileContainer folder in folders) {
                this.AddFolder(folder);
            }
        } 

        private void AddContainer(FileContainer element, List<FileContainer> list)
        {
            element.previousContainer = this;

            // Get tags that will be added recursively (_key_)
            List<KeyValuePair<string, object>> commonTags = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, object> entry in this.Tags) {
                if (entry.Key.StartsWith("_") && entry.Key.EndsWith("_"))
                    commonTags.Add(entry);
            }

            // For each child, update it's path variable and add common tags
            Queue<FileContainer> queue = new Queue<FileContainer>();
            queue.Enqueue(element);
            while (queue.Count > 0) {
                FileContainer child = queue.Dequeue();
                child.Path = this.Path + child.Path;                    // Update path
                commonTags.ForEach(e => child.Tags[e.Key] = e.Value);    // Add common tags

                foreach (FileContainer subchild in child.files)
                    queue.Enqueue(subchild);

                foreach (FileContainer subchild in child.folders)
                    queue.Enqueue(subchild);
            }

            // If the name matches, replace
            for (int i = 0; i < list.Count; i++) {
                if (list[i].Name == element.Name) {
                    list[i] = element;
                    return;
                }
            }

            list.Add(element);
        }

        /// <summary>
        /// Search an element by path.
        /// </summary>
        /// <param name="path">Path to search.</param>
        /// <returns>File/Folder or null if not found.</returns>
        public FileContainer SearchFile(string path)
        {            
            if (!path.StartsWith(this.Path))
                return null;

            if (path == this.Path)
                return this;
            
            foreach (FileContainer f in this.files) {
                FileContainer el = f.SearchFile(path);
                if (el != null)
                    return el;
            }

            foreach (FileContainer f in this.folders) {
                FileContainer el = f.SearchFile(path);
                if (el != null)
                    return el;
            }

            return null;
        }
        
        public void Clear()
        {
            this.files.Clear();
            this.folders.Clear();
        }

        public FileContainer[] GetFilesRecursive(bool returnsFileChildren)
        {
            List<FileContainer> list = new List<FileContainer>();
            list.AddRange(this.files);

            if (returnsFileChildren) {
                foreach (FileContainer f in this.files)
                    list.AddRange(f.GetFilesRecursive(returnsFileChildren));
            }

            foreach (FileContainer f in this.folders)
                list.AddRange(f.GetFilesRecursive(returnsFileChildren));

            return list.ToArray();
        }

        public void AssignTagsRecursive(IDictionary<string, object> tags)
        {
            foreach (KeyValuePair<string, object> entry in tags)
                if (!this.tags.ContainsKey(entry.Key))
                    this.Tags.Add(entry);

            foreach (FileContainer subfile in this.files)
                subfile.AssignTagsRecursive(tags);

            foreach (FileContainer subfolder in this.folders)
                subfolder.AssignTagsRecursive(tags);
        }

        public class FileContainerCollection : ReadOnlyCollection<FileContainer>
        {
            public FileContainerCollection(IList<FileContainer> list)
                : base(list)
            {
            }

            public FileContainer this[string name] {
                get {
                    foreach (var cont  in this)
                        if (cont.Name == name)
                            return cont;

                    return null;
                }
            }
        }
    }
}