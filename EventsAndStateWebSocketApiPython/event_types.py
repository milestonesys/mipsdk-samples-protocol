"""
This file contains a subset of the event types that can be used in a subscription
Call the REST API to get a full list of events currently available in the system: https://{{host}}/api/rest/v1/eventTypes/
"""

motion_started = "6eb95dd6-7ccc-4bce-99f8-af0d0b582d77"
motion_stopped = "6f55a7a7-d21c-4629-ac18-af1975e395a2"

recording_started = "4577f552-765a-438c-bc7d-e5ff1f754bc3"
recording_stopped = "79a94f89-92de-4fca-8a43-5561d407423d"

live_feed_terminated = "66bdd008-b856-44b6-b385-b1f6c3f76f1b"
live_feed_terminated = "eeda47ff-4f3d-459e-8143-69896c7c74ad"

communication_started = "dd3e6464-7dc0-405a-a92f-6150587563e8"
communication_stopped = "0ee90664-2924-42a0-a816-4129d0ecabdc"
communication_error = "a334af1c-4b4b-4957-9e5f-ab8ca07feab6"

communication_hw_started = "0553c396-5e16-4c22-b3d1-f548e42dfbb4"
communication_hw_stopped = "63ea1f06-5a83-4f39-9fab-49959fde7b66"
communication_hw_error = "6baad64b-c395-4f52-b6a3-dd1b64aa2f0f"

output_activated = "7a78f5bb-d8c3-4997-89b7-cae72713b7db"
output_deactivated = "35742498-bcc5-4f0a-9800-827c9388d1cd"
