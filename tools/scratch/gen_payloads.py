import sys
sys.path.insert(0, '.')
from pyatv.protocols.companion.plist_payloads.rti_text_operations import get_rti_clear_text_payload, get_rti_input_text_payload

session_uuid = bytes(range(16))
clear_payload = get_rti_clear_text_payload(session_uuid)
insert_payload = get_rti_input_text_payload(session_uuid, 'hi')
print('CLEAR:', clear_payload.hex())
print('INSERT:', insert_payload.hex())
