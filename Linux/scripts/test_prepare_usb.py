import importlib.util
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest

spec = importlib.util.spec_from_file_location('usb', Path(__file__).with_name('prepare-usb.py'))
usb = importlib.util.module_from_spec(spec)
spec.loader.exec_module(usb)


class PrepareTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.device = self.root / 'devices/usb1/1-2'
        interface = self.device / '1-2:1.0'
        interface.mkdir(parents=True)
        (self.device / 'idVendor').write_text('2ca3\n')
        (self.device / 'idProduct').write_text('0020\n')
        self.nic = self.root / 'class/net/enx123'
        self.nic.mkdir(parents=True)
        (self.nic / 'ifindex').write_text('7\n')
        (self.nic / 'device').symlink_to(interface)
        self.commands = []
        self.addresses = []

    def run_command(self, args, **kwargs):
        self.commands.append(args)
        return SimpleNamespace(stdout=json.dumps(self.addresses))

    def configure(self):
        usb.configure('enx123', 7, self.run_command, self.root)

    def test_only_adds_exact_dji_address_and_link_up(self):
        self.configure()
        self.assertEqual(self.commands[1:], [
            ['/usr/sbin/ip', 'link', 'set', 'dev', 'enx123', 'up'],
            ['/usr/sbin/ip', 'address', 'add', '192.168.60.1/24', 'dev', 'enx123']])

    def test_wrong_vendor_no_commands(self):
        (self.device / 'idVendor').write_text('1234')
        with self.assertRaises(ValueError): self.configure()
        self.assertEqual(self.commands, [])

    def test_wrong_product_no_commands(self):
        (self.device / 'idProduct').write_text('9999')
        with self.assertRaises(ValueError): self.configure()
        self.assertEqual(self.commands, [])

    def test_recycled_interface_index_no_commands(self):
        (self.nic / 'ifindex').write_text('9')
        with self.assertRaises(ValueError): self.configure()
        self.assertEqual(self.commands, [])

    def test_invalid_names(self):
        for name in ('../enx123', 'eth0;id', '..', 'a name'):
            with self.assertRaises(ValueError): usb.validate(name, 7, self.root)

    def test_other_adapter_conflict_no_mutation(self):
        self.addresses = [{'ifname': 'wlan0', 'addr_info': [{'local': '192.168.60.5', 'prefixlen': 24}]}]
        with self.assertRaises(ValueError): self.configure()
        self.assertEqual(len(self.commands), 1)

    def test_different_address_is_not_overwritten(self):
        self.addresses = [{'ifname': 'enx123', 'addr_info': [{'local': '10.0.0.3', 'prefixlen': 24}]}]
        with self.assertRaises(ValueError): self.configure()
        self.assertEqual(len(self.commands), 1)

    def test_existing_configuration_is_idempotent(self):
        self.addresses = [{'ifname': 'enx123', 'addr_info': [{'local': '192.168.60.1', 'prefixlen': 24}]}]
        self.configure()
        self.assertEqual(len(self.commands), 2)

    def test_similarly_named_non_dji_is_rejected(self):
        (self.nic / 'device').unlink()
        with self.assertRaises(OSError): self.configure()
        self.assertEqual(self.commands, [])


if __name__ == '__main__': unittest.main()
