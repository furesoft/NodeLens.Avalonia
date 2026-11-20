<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9003-000000000001" name="expressions">
  <nodes>
    <node id="d0000000-0000-4000-8003-000000000001" concept="Decl.If">
      <children role="condition">
        <node id="d0000000-0000-4000-8003-000000000101" concept="Decl.Binary">
          <properties>
            <operator>&lt;</operator>
          </properties>
          <children role="left">
            <node id="d0000000-0000-4000-8003-000000000201" concept="Decl.Int">
              <properties>
                <value>1</value>
              </properties>
            </node>
          </children>
          <children role="right">
            <node id="d0000000-0000-4000-8003-000000000202" concept="Decl.Int">
              <properties>
                <value>2</value>
              </properties>
            </node>
          </children>
        </node>
      </children>
      <children role="then">
        <node id="d0000000-0000-4000-8003-000000000102" concept="Decl.Int">
          <properties>
            <value>3</value>
          </properties>
        </node>
      </children>
      <children role="else">
        <node id="d0000000-0000-4000-8003-000000000103" concept="Decl.Int">
          <properties>
            <value>4</value>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>