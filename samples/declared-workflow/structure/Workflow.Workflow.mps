<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="433b7a53-d733-43b5-b36b-e91f3dca15b5" name="definition1">
  <nodes>
    <node id="a48d0680-96a9-4c83-80c1-63f38e8f0205" concept="Structure.Concept" name="Workflow.Workflow">
      <properties>
        <key>Workflow.Workflow</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>One runnable sequence of steps.</description>
        <nameSlot>name</nameSlot>
      </properties>
      <children role="properties">
        <node id="5d52fb42-bf12-4c29-884d-03d718cc7299" concept="Structure.PropertyDecl" name="name">
          <properties>
            <name>name</name>
            <typeName>string</typeName>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
      </children>
      <children role="children">
        <node id="c080f701-abd4-4207-a6c2-c0d9ba5b91e4" concept="Structure.ChildDecl" name="first">
          <properties>
            <name>first</name>
            <childConceptKey>Workflow.Step</childConceptKey>
            <cardinality>0..1</cardinality>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>