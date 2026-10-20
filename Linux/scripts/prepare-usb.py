#!/usr/bin/python3
"""Configure only a verified DJI USB network interface. Never changes routes/DNS elsewhere."""
import json
import os
from pathlib import Path
import re
import subprocess
import sys


def validate(name, index, root=Path('/sys')):
    if not re.fullmatch(r'[a-zA-Z0-9_.-]{1,15}', name) or name in ('.', '..'):
        raise ValueError('Invalid interface name')
    nic = root / 'class/net' / name
    if int((nic / 'ifindex').read_text()) != int(index) or int(index) <= 0:
        raise ValueError('USB interface changed; reconnect')
    node = (nic / 'device').resolve(strict=True)
    if not node.is_relative_to(root.resolve()):
        raise ValueError('Invalid sysfs path')
    for parent in (node, *node.parents):
        vendor, product = parent / 'idVendor', parent / 'idProduct'
        if vendor.is_file() and product.is_file():
            if (vendor.read_text().strip().lower(), product.read_text().strip().lower()) == ('2ca3', '0020'):
                return
            break
    raise ValueError('Not a DJI Goggles USB adapter')


def configure(name, index, run=subprocess.run, root=Path('/sys'), ip='/usr/sbin/ip'):
    validate(name, index, root)
    state = json.loads(run([ip, '-j', '-4', 'address', 'show'], check=True, capture_output=True, text=True).stdout)
    present = False
    for nic in state:
        for address in nic.get('addr_info', []):
            local = address.get('local', '')
            if nic['ifname'] != name and local.startswith('192.168.60.'):
                raise ValueError('Another adapter already uses 192.168.60.x; no changes made')
            if nic['ifname'] == name:
                if local != '192.168.60.1' or address.get('prefixlen') != 24:
                    raise ValueError('DJI adapter has another address; no changes made. See the Linux guide')
                present = True
    validate(name, index, root)
    run([ip, 'link', 'set', 'dev', name, 'up'], check=True)
    if not present:
        validate(name, index, root)
        run([ip, 'address', 'add', '192.168.60.1/24', 'dev', name], check=True)


if __name__ == '__main__':
    try:
        if os.geteuid() != 0 or len(sys.argv) != 3:
            raise ValueError('Use system authorization to configure the DJI USB adapter')
        configure(sys.argv[1], sys.argv[2])
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
