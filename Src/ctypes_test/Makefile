BITS ?= 64
CC = gcc
CFLAGS = -m$(BITS) -fPIC -Wall -Wextra -O2 -g 
LDFLAGS = -m$(BITS) -shared 
RM = rm -f  
TARGET_LIB = _ctypes_test_linux$(BITS).pyd

SRCS = _ctypes_test.c
OBJS = $(SRCS:.c=.o)

.PHONY: all
all: $(TARGET_LIB)

$(TARGET_LIB): $(OBJS)
	$(CC) $(LDFLAGS) -o $@ $^

.PHONY: clean
clean:
	-$(RM) $(TARGET_LIB) $(OBJS) 
