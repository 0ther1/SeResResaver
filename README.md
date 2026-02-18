# Serious Engine 2+ Resource Resaver

[![.NET Version](https://img.shields.io/badge/dotnet-8.0-purple)](https://dotnet.microsoft.com/)

**[English](README.md)** | **[Русский](README.ru.md)**

A desktop WPF application for resaving resource files for Serious Engine versions 2 and above under a new name, while automatically updating references to them in other files.

## Table of Contents

- [Features](#features)
- [Getting Started](#getting-started)
    - [Requirements](#requirements)
    - [Installation & Setup](#installation-setup)
- [Usage](#usage)

## Features

- **Batch Renaming:** Quickly rename multiple files by replacing substrings within their original names.
- **Reference Search & Update:** Automatically scan for files that contain references to the resources being resaved, and update those references.
- **Resaving:** Save the resource files with their new names, including updating internal references to other resources within the same batch.

## Getting Started

### Requirements

- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) is required to run the portable version.

### Installation & Setup

1.  **Download the Build:**  
    [Download the Latest Version](https://github.com/0ther1/SeResResaver/releases/latest)  
    Or navigate to the [Releases](https://github.com/0ther1/SeResResaver/releases) page.

2.  **Extract the Program:**  
    Unzip the contents to a convenient location on your computer.

3.  **Run the Application:**  
    Execute  **SeResResaver.exe**.

## Usage
      
1. **Select Files to Resave**
    a. Specify the root directory of the game whose files you are going to resave.
    b. Add the files you wish to resave (you can add them individually or by selecting entire folders).
    c. Rename the added files as needed (either manually or by using the batch rename feature).
    d. Optionally, mark any original files you want to be deleted after a successful resave operation.
2. **Add Files for Reference Updates**
   Add the files in which you want to update references (individually or by folder).  
   *Note*: The list will only display files that were found to contain references to the resources you are resaving.  
   *Important*: Files that are being resaved will automatically update references among themselves.
3. **Resave**
    a. From the dropdown list, select the game variant you are resaving the files for.
    b. Click the **Start** button.

Upon completion, the process will show a result: **Finished** or **Finished with errors**. If any errors occur, they will be displayed in the table for review.
