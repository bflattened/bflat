# ZeroLib sample

Basides the .NET standard library, the bflat compiler also comes with a minimal standard library that is a very minimal subset of what's in .NET.

There is no garbage collector. No exception handling. No useful collection types. Just a couple things to run at least _some_ code.

Think of it more as an art project than something useful. Building something that fits the supported envelope is more an artistic expression than anything else.

This directory contains a sample app (a snake game) that can be compiled both in the standard way (with a useful standard library) or with zerolib.

To build the sample with zerolib, run:

```console
$ bflat build --stdlib:zero
```

Most other `build` arguments still apply, so you can make things smaller with e.g. `--separate-symbols --no-pie`, or you can crosscompile with `--os:windows` and `--os:linux`.

You should see a fully selfcontained executable that is 10-50 kB in size, depending on the platform. That's the "art" part.

## Building UEFI boot applications

ZeroLib also supports the UEFI environment so you can build apps that run on bare metal with it.

To build the snake game as a UEFI boot application, run:

```console
$ mkdir -p efi/boot
$ bflat build --stdlib:zero --os:uefi -o:efi/boot/bootx64.efi
```

This will produce a `bootx64.efi` file under `efi/boot` that can be used as a boot loader.

To run this with QEMU, execute:

```console
qemu-system-x86_64 -bios /usr/OVMF/OVMF_CODE.fd -hdd fat:rw:.
```

The first parameter points the system to an EFI firmware (BIOS is the default). The second parameter tells QEMU to emulate a FAT filesystem starting at the current directory (this assumes there is a `./efi/boot/bootx64.efi` file). You may need to `sudo apt install ovmf` to get the EFI firmware for QEMU.

If you're on Windows, you can also execute this under Hyper-V.

First create a virtual disk with a FAT32 filesystem:

```console
$ diskpart
create vdisk file=disk.vhdx maximum=500
select vdisk file=disk.vhdx
attach vdisk
convert gpt
create partition efi size=100
format quick fs=fat32 label="System"
assign letter="X"
exit
$ xcopy efi\boot\BOOTX64.EFI X:\EFI\BOOT\
$ diskpart
select vdisk file=disk.vhdx
select partition 2
remove letter=X
detach vdisk
exit
```

Then create a Gen2 virtual machine with the hard disk. Make sure to disable secure boot.

