# By default, `make` produces a PYD for the current OS/processor.
# To build a specific PYD, the full command is:
#   * _ctypes_test_darwin_arm64.pyd:   make OS=darwin MACHINE=arm64
#   * _ctypes_test_darwin_x86_64.pyd:  make OS=darwin MACHINE=x86_64
#   * _ctypes_test_linux_aarch64.pyd:  make OS=linux MACHINE=aarch64
#   * _ctypes_test_linux_x86_64.pyd:   make OS=linux MACHINE=x86_64 BITS=64
#   * _ctypes_test_linux_i686.pyd:     make OS=linux MACHINE=i686 BITS=32
# Parameter OS=... may be skipped if it is the same as the host OS.
# For Windows builds, use `_ctypes_test.vcxproj` or `_ctypes_test.sln` with MSBuild:
#   * _ctypes_test_win64.pyd:          msbuild -p:Configuration=Release -p:Platform=x64
#   * _ctypes_test_win32.pyd:          msbuild -p:Configuration=Release -p:Platform=x86

OS := $(shell uname -s | tr '[:upper:]' '[:lower:]')
MACHINE ?= $(shell uname -m)
BITS ?= 64
CC = gcc
CFLAGS = -fPIC -Wall -Wextra -Wno-unused-parameter -O2 -g
LDFLAGS = -shared
RM = rm -f
PLATFORM = $(OS)_$(MACHINE)

ifeq ($(OS), darwin)
	TARGET = $(MACHINE)-none-$(OS)
	CFLAGS += --target=$(TARGET)
	LDFLAGS += --target=$(TARGET)
else
	ifeq ($(MACHINE), aarch64)
		ARCH = armv8-a
		CFLAGS += -march=$(ARCH)
		LDFLAGS += -march=$(ARCH)
	else
		CFLAGS += -m$(BITS)
		LDFLAGS += -m$(BITS)
	endif
endif

TARGET_LIB = _ctypes_test_$(PLATFORM).pyd

SRCS = _ctypes_test.c
OBJS = $(SRCS:.c=.o)

.PHONY: all
all: $(TARGET_LIB)

$(TARGET_LIB): $(OBJS)
	$(CC) $(LDFLAGS) -o $@ $^

.PHONY: clean
clean:
	-$(RM) $(TARGET_LIB) $(OBJS) 
