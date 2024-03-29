# Detachable Python: A Windows Service for Detachable Script Execution
Detachable Python is a Windows service that can execute and monitor arbitrary command line programs.
Despite its name, the service can execute any program that the user has appropriate permissions to access.
This repository contains three solutions:
* Detach Service
  * The windows service that tracks and executes jobs sent by the client
* Detach Client
  * A command line program used to submit jobs to the service
* Detach Library
  * Shared code accessed by both the service and the client

## Background and Motivation
Unlike Unix, the Windows operating system does not provide a
straightforward method to continue execution of a program after
the user has logged out of the shell.
This presents an issue when you want to initiate a long running
command over SSH, or want a process to continue executing after
you lock the computer.

For example, I have a desktop machine setup for remote access
using SSH. I would often want to start a python script that
would run for extended periods of time. When launching this
script over SSH, closing the terminal would terminate execution.
I might be able to use cloud computing to accomplish the
same task, but I have an i9 and RTX 4070 locally and want to
use those.

To solve this issue, I created the Detachable Python service
and client program.

## Example Usage
