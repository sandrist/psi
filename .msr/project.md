---
# Machine-readable fields for downstream agents / dashboards.
stage: early-research   # exploration | early-research | applied-research | productization | shipped
research-area: developer frameworks   # Agentic Discovery & Creation | Models & Foundations | Trust & safety, security | Health & Biology Sciences | Society & Economy
last-updated: 2026-05-14
---

# Platform for Situated Intelligence

**Team:** Interactive Multimodal Futures / Future Experiences / MSR Americas\
**Contributors:** Dan Bohus, Sean Andrist, Mike Barnett, Stuart Dent, John Elliott, Ashley Feniello, Don Gillett, Eric Horvitz, Mihai Jalobeanu, Danial McDuff, Kael Rowan, Patrick Sweeney, Anne Loomis Thompson

---

## What it is

__Platform for Situated Intelligence__ (or in short, \\psi, pronounced like the Greek letter) is an open, extensible framework for development and research of multimodal, integrative-AI systems. Examples include multimodal interactive systems such as social robots and embodied conversational agents, mixed-reality systems, applications for ambient intelligence or smart spaces, etc. In essence, any application that processes streaming, sensor data (such as audio, video, depth, etc.), combines multiple (AI) technologies, and operates under latency constraints can benefit from the affordances provided by the framework.

## Core idea

Any application that processes streams of data, and where timing is important, can benefit from the programming models, primitives, and tools provided by \\psi. The framework provides a stack of critical resources for developers:
- a modern, performant __infrastructure__ for working with multimodal, temporally streaming data
- a set of __tools__ for multimodal data visualization, annotation, and processing
- an ecosystem of __components__ for various sensors, processing technologies, and effectors

## Why it matters

**To the field:** While many applications of AI will be autonomous, a particularly important, yet challenging opportunity for AI is developing intelligent systems that can collaborate in a natural manner with people. Fluid human-AI interaction will require AI systems to sense, infer, and coordinate with people with the ease, speed, and effectiveness that people expect when working with each other. However, constructing multimodal, integrative-AI systems that operate effectively in the open world is still a very challenging engineering task. The engineering challenges arise primarily due to the mismatch between the requirements of the task at hand and the programming languages, infrastructures, and development tools we currently have available.

**Future directions:** We continue to use and extend this framework in our own research, for example in work on [task assistive agents](https://github.com/microsoft/psi/blob/master/Applications/Sigma/Readme.md). We also invite the broader research community to get involved! We welcome contributions in many forms: from simply using the framework and filing issues and bugs, to writing and releasing your own new components, to creating pull requests for bug fixes or new features.

## Publications & links

- [Platform for Situated Intelligence - arXiv, 2021](https://arxiv.org/abs/2103.15975)
- [GitHub: microsoft/psi](https://github.com/microsoft/psi)
- [Platform for Situated Intelligence: An open-source framework for multimodal, integrative AI - MSR Blog](https://www.microsoft.com/en-us/research/blog/platform-for-situated-intelligence-an-open-source-framework-for-multimodal-integrative-ai/)