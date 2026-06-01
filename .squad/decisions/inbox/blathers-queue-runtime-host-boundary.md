### 2026-06-01: Queue access stays in the host, not in the shared runtime

- Shared workflow definitions can name the queue that owns a lane.
- The shared runtime now accepts a queue access profile from the host to decide which queues can be started, viewed, and moved on.
- MockBusinessApp uses that profile to show business-user queue work on the admin page and move items on without teaching the shared runtime about business users.
- TestSite-style web flows keep their own queue profile, so the same runtime can support different host rules without hard-coded web or business assumptions.
