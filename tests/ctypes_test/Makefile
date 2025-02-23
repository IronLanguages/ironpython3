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
