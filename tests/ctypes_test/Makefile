OS := $(shell uname -s | tr '[:upper:]' '[:lower:]')
ARCH ?= $(shell uname -m)
BITS ?= 64
TARGET = $(ARCH)-none-$(OS)
CC = gcc
CFLAGS = -m$(BITS) -fPIC -Wall -Wextra -O2 -g 
LDFLAGS = -m$(BITS) -shared 
RM = rm -f  

ifeq ($(OS), darwin)
	CFLAGS += --target=$(TARGET)
	LDFLAGS += --target=$(TARGET)
	PLATFORM = $(OS)_$(ARCH)
else
	PLATFORM = $(OS)$(BITS)
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
